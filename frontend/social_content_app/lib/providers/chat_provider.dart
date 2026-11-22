import 'dart:async';

import 'package:flutter/foundation.dart';

import '../models/app_notification.dart';
import '../models/auth.dart';
import '../models/chat.dart';
import '../services/api_exception.dart';
import '../services/chat_realtime.dart';
import '../services/chat_service.dart';
import 'load_state.dart';

/// Stan czatu: lista rozmów, historia otwartych rozmów i wiadomości na żywo z SignalR.
/// Wysyłanie idzie przez REST (serwer sprawdza uprawnienia i rozsyła wiadomość na żywo do wszystkich uczestników),
/// odbieranie przez [ChatRealtime]. Wiadomość nadaną przez REST i jej echo z huba łączy deduplikacja po Id.
class ChatProvider extends ChangeNotifier {
  ChatProvider(this._service, this._realtime, {this.healthCheckInterval = const Duration(seconds: 30)});

  final ChatService _service;
  final ChatRealtime _realtime;
  final Duration healthCheckInterval;

  /// Ustawiane przez HomeScreen: krótki komunikat o nowej wiadomości (AppMessenger).
  void Function(String text)? onIncoming;

  /// Wywoływane przy zdarzeniu ReceiveNotification (HomeScreen przekazuje je do NotificationProvider).
  void Function(AppNotification notification)? onNotification;

  StreamSubscription<ChatMessage>? _messageSub;
  StreamSubscription<AppNotification>? _notificationSub;
  Timer? _health;
  bool _started = false;
  bool _connecting = false;
  bool _disposed = false;

  String? _meId;
  UserRole? _meRole;

  LoadState state = LoadState.idle;
  String? error;
  List<Conversation> conversations = const [];
  final Map<String, List<ChatMessage>> _messages = {};
  String? activeConversationId;

  bool get isConnected => _realtime.isConnected;
  int get totalUnread => conversations.fold(0, (sum, c) => sum + c.unreadCount);

  List<ChatMessage> messagesOf(String conversationId) => _messages[conversationId] ?? const [];

  Conversation? conversationById(String id) => conversations.where((c) => c.id == id).firstOrNull;

  bool isMine(ChatMessage m) => _meId != null && m.isMine(_meId!, _meRole!);

  /// Po zalogowaniu: pobiera rozmowy i łączy się z hubem (błąd połączenia nie blokuje aplikacji).
  Future<void> start(String userId, UserRole role) async {
    if (_started && _meId == userId && _meRole == role) return;
    await stop();
    _started = true;
    _meId = userId;
    _meRole = role;
    _messageSub = _realtime.messages.listen(_onMessage);
    _notificationSub = _realtime.notifications.listen((n) => onNotification?.call(n));
    _health = Timer.periodic(healthCheckInterval, (_) => _ensureConnected());
    await loadConversations();
    await _ensureConnected();
  }

  Future<void> stop() async {
    _started = false;
    _health?.cancel();
    _health = null;
    await _messageSub?.cancel();
    await _notificationSub?.cancel();
    _messageSub = null;
    _notificationSub = null;
    await _realtime.disconnect().catchError((_) {});
    _meId = null;
    _meRole = null;
    activeConversationId = null;
    conversations = const [];
    _messages.clear();
    state = LoadState.idle;
    error = null;
    _notify();
  }

  Future<void> _ensureConnected() async {
    if (!_started || _connecting || _realtime.isConnected) return;
    _connecting = true;
    try {
      await _realtime.connect();
      // Po (ponownym) połączeniu nadrabiamy to, co mogło ominąć.
      await loadConversations(silent: true);
      final active = activeConversationId;
      if (active != null) await loadMessages(active, silent: true);
    } catch (_) {
      // Serwer niedostępny - spróbujemy ponownie przy kolejnym sprawdzeniu.
    } finally {
      _connecting = false;
      _notify();
    }
  }

  Future<void> loadConversations({bool silent = false}) async {
    if (!silent && conversations.isEmpty) {
      state = LoadState.loading;
      _notify();
    }
    try {
      conversations = _sorted(await _service.conversations());
      state = LoadState.loaded;
      error = null;
    } on ApiException catch (e) {
      if (!silent || state != LoadState.loaded) {
        state = LoadState.failed;
        error = e.message;
      }
    }
    _notify();
  }

  Future<void> loadMessages(String conversationId, {bool silent = false}) async {
    try {
      final list = await _service.messages(conversationId);
      final merged = {for (final m in list) m.id: m};
      for (final m in messagesOf(conversationId)) {
        merged.putIfAbsent(m.id, () => m);
      }
      _messages[conversationId] = merged.values.toList()..sort((a, b) => a.sentAt.compareTo(b.sentAt));
      _notify();
    } on ApiException {
      if (!silent) rethrow;
    }
  }

  /// Otwarcie ekranu rozmowy: ładuje historię i oznacza rozmowę jako przeczytaną.
  Future<void> openConversation(String id) async {
    activeConversationId = id;
    await loadMessages(id);
    await _markRead(id);
  }

  void closeConversation(String id) {
    if (activeConversationId == id) activeConversationId = null;
  }

  /// Rzuca [ApiException] (np. 403 po polsku) - ekran pokazuje komunikat przez AppMessenger.
  Future<void> send(String conversationId, String text) async {
    final message = await _service.send(conversationId, text.trim());
    _addMessage(message);
  }

  Future<List<ChatContact>> contacts({String? search, ContactSearchMode mode = ContactSearchMode.contains}) =>
      _service.contacts(search: search, mode: mode);

  Future<Conversation> openDirect(ChatContact contact) async {
    final c = await _service.openDirect(contact.userId, contact.role);
    _upsert(c);
    return c;
  }

  Future<Conversation> createGroup(String name, List<String> managerIds) async {
    final c = await _service.createGroup(name, managerIds);
    _upsert(c);
    return c;
  }

  Future<ChatManager> chatManager() => _service.chatManager();

  Future<ChatManager> setChatManager(String? managerId) => _service.setChatManager(managerId);

  // ---- wiadomości na żywo ----

  void _onMessage(ChatMessage m) {
    final known = conversationById(m.conversationId) != null;
    if (!known) {
      // Nowa rozmowa (ktoś zaczął z nami czat) - pobierz listę, a wiadomość dołóż do historii.
      _addMessage(m, notifyUser: true);
      unawaited(loadConversations(silent: true));
      return;
    }
    _addMessage(m, notifyUser: true);
  }

  void _addMessage(ChatMessage m, {bool notifyUser = false}) {
    final list = [...messagesOf(m.conversationId)];
    if (list.any((e) => e.id == m.id)) return;
    list.add(m);
    list.sort((a, b) => a.sentAt.compareTo(b.sentAt));
    _messages[m.conversationId] = list;

    final index = conversations.indexWhere((c) => c.id == m.conversationId);
    final mine = isMine(m);
    final active = activeConversationId == m.conversationId;
    if (index >= 0) {
      final c = conversations[index];
      final unread = (mine || active) ? c.unreadCount : c.unreadCount + 1;
      final updated = [...conversations];
      updated[index] = c.copyWith(lastMessage: m, unreadCount: unread);
      conversations = _sorted(updated);
    }
    if (!mine && notifyUser && !active) {
      onIncoming?.call('${m.senderName}: ${_short(m.text)}');
    }
    if (!mine && active) unawaited(_markRead(m.conversationId));
    _notify();
  }

  void _upsert(Conversation c) {
    final updated = [...conversations.where((e) => e.id != c.id), c];
    conversations = _sorted(updated);
    _notify();
  }

  Future<void> _markRead(String id) async {
    final index = conversations.indexWhere((c) => c.id == id);
    if (index >= 0 && conversations[index].unreadCount > 0) {
      final updated = [...conversations];
      updated[index] = updated[index].copyWith(unreadCount: 0);
      conversations = updated;
      _notify();
    }
    try {
      await _service.markRead(id);
    } on ApiException {
      // Oznaczenie jako przeczytane nie jest krytyczne - wróci przy następnym odświeżeniu.
    }
  }

  static List<Conversation> _sorted(List<Conversation> list) {
    final copy = [...list];
    copy.sort((a, b) {
      final at = a.lastMessage?.sentAt;
      final bt = b.lastMessage?.sentAt;
      if (at == null && bt == null) return a.title.compareTo(b.title);
      if (at == null) return 1;
      if (bt == null) return -1;
      return bt.compareTo(at);
    });
    return copy;
  }

  static String _short(String text) => text.length <= 60 ? text : '${text.substring(0, 57)}...';

  void _notify() {
    if (!_disposed) notifyListeners();
  }

  @override
  void dispose() {
    _disposed = true;
    _health?.cancel();
    _messageSub?.cancel();
    _notificationSub?.cancel();
    super.dispose();
  }
}

import 'dart:async';

import 'package:signalr_netcore/signalr_client.dart';

import '../models/app_notification.dart';
import '../models/chat.dart';
import 'api_client.dart';

/// Kanał czasu rzeczywistego serwer -> klient (jedno połączenie SignalR dla czatu i powiadomień o postach).
abstract class ChatRealtime {
  /// Nowe wiadomości czatu (także własne - echo z innych urządzeń).
  Stream<ChatMessage> get messages;

  /// Powiadomienia o postach (nieudana publikacja, decyzja klienta, publikacja...).
  Stream<AppNotification> get notifications;

  bool get isConnected;

  /// Łączy się z hubem (idempotentne; rzuca wyjątek, gdy serwer jest nieosiągalny).
  Future<void> connect();

  Future<void> disconnect();
}

/// Implementacja na SignalR (pakiet signalr_netcore: Windows, web, Android). Token JWT idzie w ?access_token=,
/// co serwer akceptuje tylko dla ścieżki /hubs/chat. Automatyczne ponowne łączenie włączone; dodatkowo ChatProvider
/// sprawdza połączenie co kilkadziesiąt sekund i wznawia je po dłuższej przerwie.
class SignalRChatRealtime implements ChatRealtime {
  SignalRChatRealtime(this._api);

  final ApiClient _api;
  HubConnection? _connection;
  final _messages = StreamController<ChatMessage>.broadcast();
  final _notifications = StreamController<AppNotification>.broadcast();

  @override
  Stream<ChatMessage> get messages => _messages.stream;

  @override
  Stream<AppNotification> get notifications => _notifications.stream;

  @override
  bool get isConnected => _connection?.state == HubConnectionState.Connected;

  @override
  Future<void> connect() async {
    if (isConnected) return;
    await _connection?.stop().catchError((_) {});

    final connection = HubConnectionBuilder()
        .withUrl(
          '${_api.baseUrl}/hubs/chat',
          options: HttpConnectionOptions(accessTokenFactory: () async => _api.token ?? ''),
        )
        .withAutomaticReconnect()
        .build();

    connection.on('ReceiveMessage', (args) {
      final map = _firstMap(args);
      if (map != null) _messages.add(ChatMessage.fromJson(map));
    });
    connection.on('ReceiveNotification', (args) {
      final map = _firstMap(args);
      if (map != null) _notifications.add(AppNotification.fromJson(map));
    });

    await connection.start();
    _connection = connection;
  }

  static Map<String, dynamic>? _firstMap(List<Object?>? args) {
    final first = (args == null || args.isEmpty) ? null : args.first;
    return first is Map ? Map<String, dynamic>.from(first) : null;
  }

  @override
  Future<void> disconnect() async {
    final connection = _connection;
    _connection = null;
    await connection?.stop().catchError((_) {});
  }
}

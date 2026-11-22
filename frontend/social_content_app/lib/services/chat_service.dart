import '../models/auth.dart';
import '../models/chat.dart';
import 'api_client.dart';

/// REST czatu: kontakty, rozmowy, historia, wysyłanie, grupy i manager ds. czatu.
/// Wiadomości na żywo przychodzą osobno (zob. chat_realtime.dart); wysyłanie przez REST też rozsyła je na żywo
/// wszystkim uczestnikom, a błędy uprawnień wracają jako ApiException z komunikatem po polsku (403).
class ChatService {
  ChatService(this._api);

  final ApiClient _api;

  /// Kontakty dozwolone dla roli; [search] filtruje je po stronie serwera (zgodnie z [mode]), nie znajdzie się nikogo
  /// spoza reguł czatu.
  Future<List<ChatContact>> contacts({String? search, ContactSearchMode mode = ContactSearchMode.contains}) async {
    final phrase = search?.trim();
    final hasPhrase = phrase != null && phrase.isNotEmpty;
    final json = await _api.get(
      '/api/Chat/contacts',
      query: hasPhrase ? {'search': phrase, 'mode': mode.apiName} : null,
    ) as List<dynamic>;
    return json.map((e) => ChatContact.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<List<Conversation>> conversations() async {
    final json = await _api.get('/api/Chat/conversations') as List<dynamic>;
    return json.map((e) => Conversation.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<Conversation> openDirect(String userId, UserRole role) async {
    final json = await _api.post('/api/Chat/conversations/direct', {'userId': userId, 'role': role.apiName});
    return Conversation.fromJson(json as Map<String, dynamic>);
  }

  Future<Conversation> createGroup(String name, List<String> managerIds) async {
    final json = await _api.post('/api/Chat/groups', {'name': name, 'managerIds': managerIds});
    return Conversation.fromJson(json as Map<String, dynamic>);
  }

  Future<List<ChatMessage>> messages(String conversationId, {DateTime? before, int limit = 50}) async {
    final json = await _api.get(
      '/api/Chat/conversations/$conversationId/messages',
      query: {
        'limit': '$limit',
        if (before != null) 'before': before.toUtc().toIso8601String(),
      },
    ) as List<dynamic>;
    return json.map((e) => ChatMessage.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<ChatMessage> send(String conversationId, String text) async {
    final json = await _api.post('/api/Chat/conversations/$conversationId/messages', {'text': text});
    return ChatMessage.fromJson(json as Map<String, dynamic>);
  }

  Future<void> markRead(String conversationId) => _api.post('/api/Chat/conversations/$conversationId/read');

  Future<ChatManager> chatManager() async =>
      ChatManager.fromJson(await _api.get('/api/Chat/chat-manager') as Map<String, dynamic>);

  Future<ChatManager> setChatManager(String? managerId) async => ChatManager.fromJson(
      await _api.put('/api/Chat/chat-manager', {'managerId': managerId}) as Map<String, dynamic>);
}

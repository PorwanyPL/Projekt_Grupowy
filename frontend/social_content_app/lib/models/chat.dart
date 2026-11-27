import 'auth.dart';

enum ConversationKind {
  direct,
  companyGroup;

  static ConversationKind parse(String value) =>
      value.toLowerCase() == 'companygroup' ? ConversationKind.companyGroup : ConversationKind.direct;
}

/// Odpowiada ChatMessageDto (REST i zdarzenie SignalR ReceiveMessage).
class ChatMessage {
  const ChatMessage({
    required this.id,
    required this.conversationId,
    required this.senderId,
    required this.senderRole,
    required this.senderName,
    required this.text,
    required this.sentAt,
  });

  final String id;
  final String conversationId;
  final String senderId;
  final UserRole senderRole;
  final String senderName;
  final String text;
  final DateTime sentAt;

  bool isMine(String userId, UserRole role) => senderId == userId && senderRole == role;

  factory ChatMessage.fromJson(Map<String, dynamic> json) => ChatMessage(
        id: json['id'] as String,
        conversationId: json['conversationId'] as String,
        senderId: json['senderId'] as String,
        senderRole: UserRole.parse(json['senderRole'] as String),
        senderName: (json['senderName'] as String?) ?? '',
        text: (json['text'] as String?) ?? '',
        // Serwer zapisuje czas w UTC, ale bez znacznika strefy w JSON - traktujemy go jako UTC.
        sentAt: _utc(json['sentAt'] as String).toLocal(),
      );
}

DateTime _utc(String value) {
  final parsed = DateTime.parse(value);
  return parsed.isUtc ? parsed : DateTime.utc(parsed.year, parsed.month, parsed.day, parsed.hour, parsed.minute,
      parsed.second, parsed.millisecond, parsed.microsecond);
}

class ChatParticipant {
  const ChatParticipant({required this.userId, required this.role, required this.name});

  final String userId;
  final UserRole role;
  final String name;

  factory ChatParticipant.fromJson(Map<String, dynamic> json) => ChatParticipant(
        userId: json['userId'] as String,
        role: UserRole.parse(json['role'] as String),
        name: (json['name'] as String?) ?? '',
      );
}

/// Odpowiada ConversationDto.
class Conversation {
  const Conversation({
    required this.id,
    required this.kind,
    required this.title,
    required this.participants,
    this.lastMessage,
    this.unreadCount = 0,
  });

  final String id;
  final ConversationKind kind;
  final String title;
  final List<ChatParticipant> participants;
  final ChatMessage? lastMessage;
  final int unreadCount;

  bool get isGroup => kind == ConversationKind.companyGroup;

  Conversation copyWith({ChatMessage? lastMessage, int? unreadCount}) => Conversation(
        id: id,
        kind: kind,
        title: title,
        participants: participants,
        lastMessage: lastMessage ?? this.lastMessage,
        unreadCount: unreadCount ?? this.unreadCount,
      );

  factory Conversation.fromJson(Map<String, dynamic> json) => Conversation(
        id: json['id'] as String,
        kind: ConversationKind.parse(json['kind'] as String),
        title: (json['title'] as String?) ?? '',
        participants: ((json['participants'] as List<dynamic>?) ?? const [])
            .map((e) => ChatParticipant.fromJson(e as Map<String, dynamic>))
            .toList(),
        lastMessage: json['lastMessage'] == null
            ? null
            : ChatMessage.fromJson(json['lastMessage'] as Map<String, dynamic>),
        unreadCount: (json['unreadCount'] as num?)?.toInt() ?? 0,
      );
}

/// Sposób dopasowania frazy w wyszukiwarce rozmówców (zgodny z ChatContactSearchMode z backendu).
enum ContactSearchMode {
  /// Fraza w dowolnym miejscu nazwy.
  contains('Contains', 'Fragment nazwy'),

  /// Fraza w adresie e-mail.
  email('Email', 'E-mail');

  const ContactSearchMode(this.apiName, this.label);

  final String apiName;
  final String label;
}

/// Odpowiada ChatContactDto - osoba, z którą można zacząć rozmowę.
class ChatContact {
  const ChatContact({required this.userId, required this.role, required this.name, this.description, this.email});

  final String userId;
  final UserRole role;
  final String name;
  final String? description;
  final String? email;

  factory ChatContact.fromJson(Map<String, dynamic> json) => ChatContact(
        userId: json['userId'] as String,
        role: UserRole.parse(json['role'] as String),
        name: (json['name'] as String?) ?? '',
        description: json['description'] as String?,
        email: json['email'] as String?,
      );
}

/// Odpowiada ChatManagerDto.
class ChatManager {
  const ChatManager({this.managerId, this.managerName});

  final String? managerId;
  final String? managerName;

  factory ChatManager.fromJson(Map<String, dynamic> json) =>
      ChatManager(managerId: json['managerId'] as String?, managerName: json['managerName'] as String?);
}

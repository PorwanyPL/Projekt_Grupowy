/// Dokąd prowadzi powiadomienie po kliknięciu (zgodne z NotificationTarget z backendu).
enum NotificationTarget {
  none,
  post,
  chat;

  static NotificationTarget parse(String? value) => switch (value?.toLowerCase()) {
        'post' => NotificationTarget.post,
        'chat' => NotificationTarget.chat,
        _ => NotificationTarget.none,
      };
}

/// Odpowiada NotificationDto (np. o nieudanej publikacji posta albo nowej wiadomości czatu).
/// [targetType] + [targetId] mówią, co otworzyć po kliknięciu: post (GroupId posta) albo rozmowę czatu.
class AppNotification {
  const AppNotification({
    required this.id,
    required this.message,
    required this.createdAt,
    required this.isRead,
    this.postId,
    this.targetType = NotificationTarget.none,
    this.targetId,
  });

  final String id;
  final String? postId;
  final String message;
  final DateTime createdAt;
  final bool isRead;
  final NotificationTarget targetType;
  final String? targetId;

  /// Czy kliknięcie ma dokąd prowadzić.
  bool get hasTarget => targetType != NotificationTarget.none && targetId != null;

  factory AppNotification.fromJson(Map<String, dynamic> json) => AppNotification(
        id: json['id'] as String,
        postId: json['postId'] as String?,
        message: json['message'] as String,
        createdAt: DateTime.parse(json['createdAt'] as String).toLocal(),
        isRead: json['isRead'] as bool? ?? false,
        targetType: NotificationTarget.parse(json['targetType'] as String?),
        targetId: json['targetId'] as String?,
      );
}

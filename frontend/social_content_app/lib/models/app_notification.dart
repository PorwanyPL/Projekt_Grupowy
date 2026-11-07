/// Odpowiada NotificationDto (np. o nieudanej publikacji posta).
class AppNotification {
  const AppNotification({
    required this.id,
    required this.message,
    required this.createdAt,
    required this.isRead,
    this.postId,
  });

  final String id;
  final String? postId;
  final String message;
  final DateTime createdAt;
  final bool isRead;

  factory AppNotification.fromJson(Map<String, dynamic> json) => AppNotification(
        id: json['id'] as String,
        postId: json['postId'] as String?,
        message: json['message'] as String,
        createdAt: DateTime.parse(json['createdAt'] as String).toLocal(),
        isRead: json['isRead'] as bool? ?? false,
      );
}

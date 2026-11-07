import 'enums.dart';
import 'manager_user.dart';

class PictureItem {
  const PictureItem({
    required this.id,
    required this.fileName,
    required this.format,
    required this.sizeKb,
    required this.storageKey,
  });

  final String id;
  final String fileName;
  final MediaFormat format;
  final int sizeKb;

  /// Klucz pliku w magazynie serwera (GET /api/Media/{storageKey}).
  final String storageKey;

  factory PictureItem.fromJson(Map<String, dynamic> json) => PictureItem(
        id: json['id'] as String,
        fileName: json['fileName'] as String,
        format: MediaFormat.parse(json['format'] as String),
        sizeKb: (json['sizeKb'] as num).toInt(),
        storageKey: json['storageKey'] as String,
      );
}

class VideoItem {
  const VideoItem({
    required this.id,
    required this.fileName,
    required this.format,
    required this.sizeKb,
    required this.storageKey,
    this.length,
  });

  final String id;
  final String fileName;
  final MediaFormat format;
  final int sizeKb;
  final String storageKey;

  /// Czas trwania w sekundach; null, gdy serwer go nie zna.
  final int? length;

  factory VideoItem.fromJson(Map<String, dynamic> json) => VideoItem(
        id: json['id'] as String,
        fileName: json['fileName'] as String,
        format: MediaFormat.parse(json['format'] as String),
        sizeKb: (json['sizeKb'] as num).toInt(),
        storageKey: json['storageKey'] as String,
        length: (json['length'] as num?)?.toInt(),
      );
}

/// Aktywna blokada edycji posta (PostLockDto).
class PostLock {
  const PostLock({required this.holderName, required this.expiresAt, required this.heldByMe});

  final String holderName;
  final DateTime expiresAt;
  final bool heldByMe;

  factory PostLock.fromJson(Map<String, dynamic> json) => PostLock(
        holderName: json['holderName'] as String,
        expiresAt: DateTime.parse(json['expiresAt'] as String).toLocal(),
        heldByMe: json['heldByMe'] as bool? ?? false,
      );
}

/// Odpowiada PostDto z backendu.
class Post {
  const Post({
    required this.id,
    required this.title,
    required this.createdAt,
    required this.description,
    required this.visibility,
    required this.pictures,
    required this.videos,
    required this.managers,
    required this.status,
    this.scheduledAt,
    this.publishedAt,
    this.errorMessage,
    this.targetAccountId,
    this.canEdit = false,
    this.canDelete = false,
    this.lock,
  });

  final String id;
  final String title;
  final DateTime createdAt;
  final String description;
  final PostType visibility;
  final List<PictureItem> pictures;
  final List<VideoItem> videos;

  /// Autorzy i współautorzy (managerowie przypisani do konta w chwili tworzenia posta).
  final List<ManagerUser> managers;
  final PostStatus status;
  final DateTime? scheduledAt;
  final DateTime? publishedAt;
  final String? errorMessage;

  /// Konto społecznościowe, na którym utworzono post.
  final String? targetAccountId;

  /// Uprawnienia policzone przez serwer dla zalogowanego użytkownika.
  final bool canEdit;
  final bool canDelete;

  /// Aktywna blokada edycji (ktoś właśnie edytuje post).
  final PostLock? lock;

  /// Edytuje ktoś inny niż zalogowany użytkownik.
  bool get lockedByOther => lock != null && !lock!.heldByMe;

  factory Post.fromJson(Map<String, dynamic> json) => Post(
        id: json['id'] as String,
        title: json['title'] as String,
        createdAt: DateTime.parse(json['createdAt'] as String).toLocal(),
        description: json['description'] as String,
        visibility: PostType.parse(json['visibility'] as String),
        pictures: (json['pictures'] as List<dynamic>)
            .map((e) => PictureItem.fromJson(e as Map<String, dynamic>))
            .toList(),
        videos: (json['videos'] as List<dynamic>)
            .map((e) => VideoItem.fromJson(e as Map<String, dynamic>))
            .toList(),
        managers: (json['managerUsers'] as List<dynamic>)
            .map((e) => ManagerUser.fromJson(e as Map<String, dynamic>))
            .toList(),
        status: PostStatus.parse(json['status'] as String),
        scheduledAt: json['scheduledAt'] == null
            ? null
            : DateTime.parse(json['scheduledAt'] as String).toLocal(),
        publishedAt: json['publishedAt'] == null
            ? null
            : DateTime.parse(json['publishedAt'] as String).toLocal(),
        errorMessage: json['errorMessage'] as String?,
        targetAccountId: json['targetAccountId'] as String?,
        canEdit: json['canEdit'] as bool? ?? false,
        canDelete: json['canDelete'] as bool? ?? false,
        lock: json['lock'] == null
            ? null
            : PostLock.fromJson(json['lock'] as Map<String, dynamic>),
      );
}

/// Plik dodany do formularza posta. Pliki są wcześniej przesłane na serwer (POST /api/Media),
/// więc w poście zapisuje się tylko [storageKey] i nazwę. Format i rozmiar ustala serwer.
class MediaInput {
  const MediaInput({
    required this.storageKey,
    required this.fileName,
    required this.format,
    required this.sizeKb,
  });

  final String storageKey;
  final String fileName;
  final MediaFormat format;
  final int sizeKb;

  /// Odpowiedź MediaUploadDto z POST /api/Media.
  factory MediaInput.fromUploadJson(Map<String, dynamic> json) => MediaInput(
        storageKey: json['storageKey'] as String,
        fileName: json['fileName'] as String,
        format: MediaFormat.parse(json['format'] as String),
        sizeKb: (json['sizeKb'] as num).toInt(),
      );

  Map<String, dynamic> toJson() => {'storageKey': storageKey, 'fileName': fileName};
}

/// Odpowiada PostWriteDto (tworzenie i edycja). Współautorów ustawia serwer
/// (wszyscy managerowie przypisani do konta), więc nie ma ich w żądaniu.
class PostInput {
  const PostInput({
    required this.title,
    required this.description,
    required this.type,
    required this.pictures,
    required this.videos,
    required this.targetAccountId,
    this.publishNow = false,
    this.scheduledAt,
  });

  final String title;
  final String description;
  final PostType type;
  final List<MediaInput> pictures;
  final List<MediaInput> videos;
  final String targetAccountId;

  /// "Publikuj teraz"; wyklucza [scheduledAt].
  final bool publishNow;

  /// "Zaplanuj": termin w przyszłości (czas lokalny, do API idzie w UTC).
  final DateTime? scheduledAt;

  Map<String, dynamic> toJson() => {
        'title': title,
        'description': description,
        'visibility': type.apiName,
        'pictures': pictures.map((p) => p.toJson()).toList(),
        'videos': videos.map((v) => v.toJson()).toList(),
        'targetAccountId': targetAccountId,
        'publishNow': publishNow,
        if (scheduledAt != null) 'scheduledAt': scheduledAt!.toUtc().toIso8601String(),
      };
}

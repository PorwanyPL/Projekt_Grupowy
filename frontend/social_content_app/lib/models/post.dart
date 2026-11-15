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

/// Wariant posta na jednym koncie (PostTargetDto): post na kilka platform to jedna grupa wariantów.
class PostTarget {
  const PostTarget({
    required this.postId,
    required this.accountName,
    required this.status,
    this.accountId,
    this.platform,
    this.errorMessage,
  });

  final String postId;
  final String? accountId;
  final String accountName;
  final SocialPlatform? platform;
  final PostStatus status;
  final String? errorMessage;

  factory PostTarget.fromJson(Map<String, dynamic> json) => PostTarget(
        postId: json['postId'] as String,
        accountId: json['accountId'] as String?,
        accountName: (json['accountName'] as String?) ?? '',
        platform: json['platform'] == null ? null : SocialPlatform.parse(json['platform'] as String),
        status: PostStatus.parse(json['status'] as String),
        errorMessage: json['errorMessage'] as String?,
      );
}

/// Odpowiada PostDto z backendu. [id] to identyfikator grupy wariantów (jeden logiczny post).
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
    this.canApprove = false,
    this.lock,
    this.tags = const [],
    this.targets = const [],
    this.counterpartName,
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

  /// Klient może zatwierdzić/odrzucić post (czeka na akceptację na jego koncie).
  final bool canApprove;

  /// Aktywna blokada edycji (ktoś właśnie edytuje post).
  final PostLock? lock;

  /// Tagi treści (filtrowanie w kalendarzu i na liście).
  final List<String> tags;

  /// Warianty posta na poszczególnych kontach/platformach (jedna pozycja na konto).
  final List<PostTarget> targets;

  /// Klient (dla firmy i managera - alias lub imię i nazwisko) albo firma (dla klienta), której dotyczy post.
  final String? counterpartName;

  /// Platformy posta bez powtórzeń, w stałej kolejności (ikony na liście i w kalendarzu).
  List<SocialPlatform> get platforms {
    final set = <SocialPlatform>{for (final t in targets) if (t.platform != null) t.platform!};
    return [for (final p in SocialPlatform.values) if (set.contains(p)) p];
  }

  /// Identyfikatory kont, na których jest post.
  List<String> get accountIds => [
        for (final t in targets)
          if (t.accountId != null) t.accountId!,
        if (targets.isEmpty && targetAccountId != null) targetAccountId!,
      ];

  /// Dzień w kalendarzu: publikacja dla opublikowanych, termin dla zaplanowanych, w pozostałych przypadkach
  /// dzień utworzenia (tak samo liczy serwer przy filtrowaniu po dacie).
  DateTime get calendarDate {
    if (status == PostStatus.published && publishedAt != null) return publishedAt!;
    return scheduledAt ?? createdAt;
  }

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
        canApprove: json['canApprove'] as bool? ?? false,
        lock: json['lock'] == null
            ? null
            : PostLock.fromJson(json['lock'] as Map<String, dynamic>),
        tags: ((json['tags'] as List<dynamic>?) ?? const []).map((e) => e as String).toList(),
        targets: ((json['targets'] as List<dynamic>?) ?? const [])
            .map((e) => PostTarget.fromJson(e as Map<String, dynamic>))
            .toList(),
        counterpartName: json['counterpartName'] as String?,
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
    this.localPath,
  });

  final String storageKey;
  final String fileName;
  final MediaFormat format;
  final int sizeKb;

  /// Ścieżka pliku wybranego z dysku (tylko lokalnie, nie trafia do API) - podgląd wideo odtwarza ją od razu,
  /// bez pobierania świeżo przesłanego pliku z serwera.
  final String? localPath;

  MediaInput withLocalPath(String? path) =>
      MediaInput(storageKey: storageKey, fileName: fileName, format: format, sizeKb: sizeKb, localPath: path);

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
    required this.targetAccountIds,
    this.tags = const [],
    this.publishNow = false,
    this.scheduledAt,
  });

  final String title;
  final String description;
  final PostType type;
  final List<MediaInput> pictures;
  final List<MediaInput> videos;
  /// Konta (platformy), na których ma powstać post - razem tworzą jeden logiczny post.
  final List<String> targetAccountIds;

  /// Tagi treści (maks. 10, do 30 znaków).
  final List<String> tags;

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
        'targetAccountIds': targetAccountIds,
        'tags': tags,
        'publishNow': publishNow,
        if (scheduledAt != null) 'scheduledAt': scheduledAt!.toUtc().toIso8601String(),
      };
}

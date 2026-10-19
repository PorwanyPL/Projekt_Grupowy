import 'enums.dart';
import 'manager_user.dart';

class PictureItem {
  const PictureItem({
    required this.id,
    required this.fileName,
    required this.format,
    required this.sizeKb,
  });

  final String id;
  final String fileName;
  final MediaFormat format;
  final int sizeKb;

  factory PictureItem.fromJson(Map<String, dynamic> json) => PictureItem(
        id: json['id'] as String,
        fileName: json['fileName'] as String,
        format: MediaFormat.parse(json['format'] as String),
        sizeKb: (json['sizeKb'] as num).toInt(),
      );
}

class VideoItem {
  const VideoItem({
    required this.id,
    required this.fileName,
    required this.format,
    required this.length,
  });

  final String id;
  final String fileName;
  final MediaFormat format;

  /// Czas trwania w sekundach.
  final int length;

  factory VideoItem.fromJson(Map<String, dynamic> json) => VideoItem(
        id: json['id'] as String,
        fileName: json['fileName'] as String,
        format: MediaFormat.parse(json['format'] as String),
        length: (json['length'] as num).toInt(),
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
  });

  final String id;
  final String title;
  final DateTime createdAt;
  final String description;
  final PostType visibility;
  final List<PictureItem> pictures;
  final List<VideoItem> videos;
  final List<ManagerUser> managers;

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
      );
}

/// Plik w formularzu: [amount] to rozmiar w KB (zdjęcie) lub długość w sekundach (wideo).
class MediaInput {
  const MediaInput({
    required this.fileName,
    required this.format,
    required this.amount,
  });

  final String fileName;
  final MediaFormat format;
  final int amount;
}

/// Odpowiada PostWriteDto (tworzenie i edycja).
class PostInput {
  const PostInput({
    required this.title,
    required this.description,
    required this.type,
    required this.pictures,
    required this.videos,
    required this.managerIds,
  });

  final String title;
  final String description;
  final PostType type;
  final List<MediaInput> pictures;
  final List<MediaInput> videos;
  final List<String> managerIds;

  Map<String, dynamic> toJson() => {
        'title': title,
        'description': description,
        'visibility': type.apiName,
        'pictures': pictures
            .map((p) => {
                  'fileName': p.fileName,
                  'format': p.format.apiName,
                  'sizeKb': p.amount,
                })
            .toList(),
        'videos': videos
            .map((v) => {
                  'fileName': v.fileName,
                  'format': v.format.apiName,
                  'length': v.amount,
                })
            .toList(),
        'managerUserIds': managerIds,
      };
}

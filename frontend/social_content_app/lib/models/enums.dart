enum MediaFormat {
  mp3,
  mp4,
  flac,
  png,
  jpeg,
  jpg,
  gif,
  webp;

  static const List<MediaFormat> imageFormats = [
    MediaFormat.png,
    MediaFormat.jpeg,
    MediaFormat.jpg,
    MediaFormat.gif,
    MediaFormat.webp,
  ];

  static const List<MediaFormat> videoFormats = [
    MediaFormat.mp4,
    MediaFormat.mp3,
    MediaFormat.flac,
  ];

  /// Nazwa zgodna z backendem (np. "Png").
  String get apiName => name[0].toUpperCase() + name.substring(1);

  String get label => name;

  static MediaFormat parse(String value) => MediaFormat.values.firstWhere(
        (e) => e.name == value.toLowerCase(),
        orElse: () => throw FormatException('Nieznany format: $value'),
      );
}

enum PostType {
  text,
  photo,
  video;

  String get apiName => name[0].toUpperCase() + name.substring(1);

  String get label {
    switch (this) {
      case PostType.text:
        return 'Tekst';
      case PostType.photo:
        return 'Zdjęcie';
      case PostType.video:
        return 'Wideo';
    }
  }

  static PostType parse(String value) => PostType.values.firstWhere(
        (e) => e.name == value.toLowerCase(),
        orElse: () => throw FormatException('Nieznany typ posta: $value'),
      );
}

enum SocialPlatform {
  facebook,
  instagram,
  tikTok;

  String get apiName => name[0].toUpperCase() + name.substring(1);

  String get label {
    switch (this) {
      case SocialPlatform.facebook:
        return 'Facebook';
      case SocialPlatform.instagram:
        return 'Instagram';
      case SocialPlatform.tikTok:
        return 'TikTok';
    }
  }

  static SocialPlatform parse(String value) =>
      SocialPlatform.values.firstWhere(
        (e) => e.name == value.toLowerCase() || e.name.toLowerCase() == value.toLowerCase(),
        orElse: () => throw FormatException('Nieznana platforma: $value'),
      );
}

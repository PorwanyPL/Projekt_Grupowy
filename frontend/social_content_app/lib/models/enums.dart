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

/// Status publikacji posta (PostStatus z backendu).
enum PostStatus {
  draft,
  scheduled,
  publishing,
  published,
  failed,
  awaitingApproval,
  rejected;

  /// Nazwa zgodna z backendem (np. "AwaitingApproval") - parametr filtra `status`.
  String get apiName => name[0].toUpperCase() + name.substring(1);

  String get label {
    switch (this) {
      case PostStatus.draft:
        return 'Szkic';
      case PostStatus.scheduled:
        return 'Zaplanowany';
      case PostStatus.publishing:
        return 'Publikowanie';
      case PostStatus.published:
        return 'Opublikowany';
      case PostStatus.failed:
        return 'Błąd';
      case PostStatus.awaitingApproval:
        return 'Czeka na akceptację';
      case PostStatus.rejected:
        return 'Odrzucony';
    }
  }

  static PostStatus parse(String value) => PostStatus.values.firstWhere(
        (e) => e.name.toLowerCase() == value.toLowerCase(), // także AwaitingApproval
        orElse: () => throw FormatException('Nieznany status posta: $value'),
      );
}

/// Kto zatwierdza posty przed publikacją (PostApprovalMode z backendu; ustawia klient per firma).
enum PostApprovalMode {
  manager,
  client;

  String get apiName => name[0].toUpperCase() + name.substring(1);

  String get label => this == PostApprovalMode.client
      ? 'Zatwierdzam posty przed publikacją'
      : 'Deleguję decyzję managerowi firmy';

  static PostApprovalMode parse(String? value) => PostApprovalMode.values.firstWhere(
        (e) => e.name == (value ?? '').toLowerCase(),
        orElse: () => PostApprovalMode.manager,
      );
}

/// Stan zaproszenia firmy do obsługi konta (ManagementStatus z backendu).
enum ManagementStatus {
  invited,
  accepted,
  declined;

  String get label {
    switch (this) {
      case ManagementStatus.invited:
        return 'Zaproszenie oczekuje';
      case ManagementStatus.accepted:
        return 'Obsługiwane przez firmę';
      case ManagementStatus.declined:
        return 'Zaproszenie odrzucone';
    }
  }

  static ManagementStatus parse(String? value) => ManagementStatus.values.firstWhere(
        (e) => e.name == (value ?? '').toLowerCase(),
        orElse: () => ManagementStatus.invited,
      );
}

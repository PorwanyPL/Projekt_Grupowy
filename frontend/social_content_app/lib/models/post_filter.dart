import 'enums.dart';

/// Filtry listy postów i kalendarza (parametry zapytania GET /api/Posts).
/// Kategorie łączą się koniunkcją (AND), wartości w obrębie kategorii alternatywą (OR).
class PostFilter {
  const PostFilter({
    this.statuses = const {},
    this.tags = const {},
    this.accountIds = const {},
    this.platforms = const {},
    this.managerId,
    this.from,
    this.to,
    this.type,
    this.accountId,
  });

  final Set<PostStatus> statuses;
  final Set<String> tags;

  /// Wybrane profile (konta społecznościowe).
  final Set<String> accountIds;
  final Set<SocialPlatform> platforms;

  /// Osoba przypisana do posta (manager).
  final String? managerId;

  /// Zakres dat (czas lokalny); serwer porównuje z dniem publikacji/terminem/utworzenia.
  final DateTime? from;
  final DateTime? to;

  final PostType? type;

  /// Posty jednego konta (ekran szczegółów konta).
  final String? accountId;

  static const empty = PostFilter();

  bool get isEmpty =>
      statuses.isEmpty &&
      tags.isEmpty &&
      accountIds.isEmpty &&
      platforms.isEmpty &&
      managerId == null &&
      from == null &&
      to == null &&
      type == null &&
      accountId == null;

  /// Liczba aktywnych kategorii filtrów (plakietka na przycisku "Filtry"); zakres dat liczy się jako jedna.
  int get activeCount =>
      (statuses.isEmpty ? 0 : 1) +
      (tags.isEmpty ? 0 : 1) +
      (accountIds.isEmpty ? 0 : 1) +
      (platforms.isEmpty ? 0 : 1) +
      (managerId == null ? 0 : 1) +
      (from == null && to == null ? 0 : 1);

  PostFilter copyWith({
    Set<PostStatus>? statuses,
    Set<String>? tags,
    Set<String>? accountIds,
    Set<SocialPlatform>? platforms,
    String? managerId,
    bool clearManager = false,
    DateTime? from,
    DateTime? to,
    bool clearDates = false,
    PostType? type,
    bool clearType = false,
    String? accountId,
  }) =>
      PostFilter(
        statuses: statuses ?? this.statuses,
        tags: tags ?? this.tags,
        accountIds: accountIds ?? this.accountIds,
        platforms: platforms ?? this.platforms,
        managerId: clearManager ? null : (managerId ?? this.managerId),
        from: clearDates ? null : (from ?? this.from),
        to: clearDates ? null : (to ?? this.to),
        type: clearType ? null : (type ?? this.type),
        accountId: accountId ?? this.accountId,
      );

  /// Parametry zapytania; listy idą jako powtórzone klucze (status=Draft&status=Scheduled).
  Map<String, dynamic> toQuery() => {
        if (type != null) 'type': type!.apiName,
        if (accountId != null) 'accountId': accountId,
        if (managerId != null) 'managerId': managerId,
        if (statuses.isNotEmpty) 'status': [for (final s in statuses) s.apiName],
        if (tags.isNotEmpty) 'tag': tags.toList(),
        if (accountIds.isNotEmpty) 'accountIds': accountIds.toList(),
        if (platforms.isNotEmpty) 'platform': [for (final p in platforms) p.apiName],
        if (from != null) 'from': from!.toUtc().toIso8601String(),
        if (to != null) 'to': to!.toUtc().toIso8601String(),
      };
}

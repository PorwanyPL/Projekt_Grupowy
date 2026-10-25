import 'enums.dart';
import 'manager_user.dart';

/// Odpowiada SocialAccountDto z backendu (bez tokenów - serwer ich nie zwraca).
class SocialAccount {
  const SocialAccount({
    required this.id,
    required this.accountId,
    required this.platform,
    required this.externalPlatformId,
    required this.accountName,
    required this.tokenExpiresAt,
    required this.isActive,
    required this.errorMessage,
    required this.hasRefreshToken,
    required this.managers,
  });

  final String id;
  final String accountId;
  final SocialPlatform platform;
  final String externalPlatformId;
  final String accountName;
  final DateTime tokenExpiresAt;
  final bool isActive;
  final String? errorMessage;
  final bool hasRefreshToken;
  final List<ManagerUser> managers;

  factory SocialAccount.fromJson(Map<String, dynamic> json) => SocialAccount(
        id: json['id'] as String,
        accountId: json['accountId'] as String,
        platform: SocialPlatform.parse(json['platform'] as String),
        externalPlatformId: json['externalPlatformId'] as String,
        accountName: json['accountName'] as String,
        tokenExpiresAt: DateTime.parse(json['tokenExpiresAt'] as String).toLocal(),
        isActive: json['isActive'] as bool,
        errorMessage: json['errorMessage'] as String?,
        hasRefreshToken: json['hasRefreshToken'] as bool? ?? false,
        managers: ((json['managerUsers'] as List<dynamic>?) ?? const [])
            .map((e) => ManagerUser.fromJson(e as Map<String, dynamic>))
            .toList(),
      );
}

/// Odpowiada SocialAccountWriteDto. Puste tokeny przy edycji = bez zmiany.
class SocialAccountInput {
  const SocialAccountInput({
    required this.accountId,
    required this.platform,
    required this.externalPlatformId,
    required this.accountName,
    this.accessToken,
    this.refreshToken,
    required this.tokenExpiresAt,
    required this.isActive,
    required this.managerUserIds,
  });

  final String accountId;
  final SocialPlatform platform;
  final String externalPlatformId;
  final String accountName;
  final String? accessToken;
  final String? refreshToken;
  final DateTime tokenExpiresAt;
  final bool isActive;
  final List<String> managerUserIds;

  Map<String, dynamic> toJson() => {
        'accountId': accountId,
        'platform': platform.apiName,
        'externalPlatformId': externalPlatformId,
        'accountName': accountName,
        if (accessToken != null) 'accessToken': accessToken,
        if (refreshToken != null) 'refreshToken': refreshToken,
        'tokenExpiresAt': tokenExpiresAt.toUtc().toIso8601String(),
        'isActive': isActive,
        'managerUserIds': managerUserIds,
      };
}

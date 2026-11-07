/// Role zgodne z claimem "role" i polem role w UserProfileDto z backendu.
enum UserRole {
  companyUser,
  managerUser,
  clientUser;

  /// Nazwa zgodna z backendem (np. "CompanyUser").
  String get apiName => name[0].toUpperCase() + name.substring(1);

  String get label {
    switch (this) {
      case UserRole.companyUser:
        return 'Administrator firmy';
      case UserRole.managerUser:
        return 'Manager';
      case UserRole.clientUser:
        return 'Klient';
    }
  }

  static UserRole parse(String value) => UserRole.values.firstWhere(
        (e) => e.name.toLowerCase() == value.toLowerCase(),
        orElse: () => throw FormatException('Nieznana rola: $value'),
      );
}

/// Odpowiada UserProfileDto z backendu.
class UserProfile {
  const UserProfile({
    required this.id,
    required this.role,
    required this.email,
    required this.displayName,
    this.companyId,
    this.age,
    this.description,
  });

  /// CompanyId (firma), Id managera albo Id klienta - zależnie od roli.
  final String id;
  final UserRole role;
  final String email;

  /// Nazwa firmy (CompanyUser) albo imię i nazwisko.
  final String displayName;
  final String? companyId;
  final int? age;
  final String? description;

  bool get isCompany => role == UserRole.companyUser;
  bool get isManager => role == UserRole.managerUser;
  bool get isClient => role == UserRole.clientUser;

  factory UserProfile.fromJson(Map<String, dynamic> json) => UserProfile(
        id: json['id'] as String,
        role: UserRole.parse(json['role'] as String),
        email: json['email'] as String,
        displayName: json['displayName'] as String,
        companyId: json['companyId'] as String?,
        age: (json['age'] as num?)?.toInt(),
        description: json['description'] as String?,
      );
}

/// Odpowiada AuthResponseDto z backendu.
class AuthSession {
  const AuthSession({
    required this.accessToken,
    required this.expiresAt,
    required this.user,
  });

  final String accessToken;
  final DateTime expiresAt;
  final UserProfile user;

  bool get isExpired => !expiresAt.isAfter(DateTime.now());

  factory AuthSession.fromJson(Map<String, dynamic> json) => AuthSession(
        accessToken: json['accessToken'] as String,
        expiresAt: DateTime.parse(json['expiresAt'] as String).toLocal(),
        user: UserProfile.fromJson(json['user'] as Map<String, dynamic>),
      );
}

/// Odpowiada RegisterCompanyDto (rejestracja administratora firmy).
class RegisterCompanyInput {
  const RegisterCompanyInput({
    required this.name,
    required this.email,
    required this.password,
  });

  final String name;
  final String email;
  final String password;

  Map<String, dynamic> toJson() => {
        'name': name,
        'email': email,
        'password': password,
      };
}

/// Odpowiada RegisterClientDto (samodzielna rejestracja klienta).
class RegisterClientInput {
  const RegisterClientInput({
    required this.firstName,
    required this.lastName,
    required this.age,
    required this.description,
    required this.email,
    required this.password,
  });

  final String firstName;
  final String lastName;
  final int age;
  final String description;
  final String email;
  final String password;

  Map<String, dynamic> toJson() => {
        'firstName': firstName,
        'lastName': lastName,
        'age': age,
        'description': description,
        'email': email,
        'password': password,
      };
}

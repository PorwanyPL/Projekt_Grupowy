/// Odpowiada ManagerProfileDto z backendu.
class ManagerProfile {
  const ManagerProfile({
    required this.id,
    required this.email,
    required this.firstName,
    required this.lastName,
    required this.age,
    required this.description,
  });

  final String id;
  final String email;
  final String firstName;
  final String lastName;
  final int age;
  final String description;

  String get fullName => '$firstName $lastName';

  factory ManagerProfile.fromJson(Map<String, dynamic> json) => ManagerProfile(
        id: json['id'] as String,
        email: json['email'] as String,
        firstName: json['firstName'] as String,
        lastName: json['lastName'] as String,
        age: (json['age'] as num).toInt(),
        description: json['description'] as String,
      );
}

/// Odpowiada AuthResponseDto z backendu.
class AuthSession {
  const AuthSession({
    required this.accessToken,
    required this.expiresAt,
    required this.manager,
  });

  final String accessToken;
  final DateTime expiresAt;
  final ManagerProfile manager;

  bool get isExpired => !expiresAt.isAfter(DateTime.now());

  factory AuthSession.fromJson(Map<String, dynamic> json) => AuthSession(
        accessToken: json['accessToken'] as String,
        expiresAt: DateTime.parse(json['expiresAt'] as String).toLocal(),
        manager: ManagerProfile.fromJson(json['manager'] as Map<String, dynamic>),
      );
}

/// Odpowiada RegisterDto (dane managera + e-mail i hasło).
class RegisterInput {
  const RegisterInput({
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

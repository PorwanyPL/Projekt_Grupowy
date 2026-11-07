/// Odpowiada ManagerUserDto z backendu.
class ManagerUser {
  const ManagerUser({
    required this.id,
    required this.firstName,
    required this.lastName,
    required this.age,
    required this.description,
    this.socialAccountIds = const [],
    this.hasLoginAccount,
    this.isAccessBlocked,
    this.email,
  });

  final String id;
  final String firstName;
  final String lastName;
  final int age;
  final String description;

  /// Konta społecznościowe, do których manager jest przypisany (relacja n - m).
  final List<String> socialAccountIds;

  /// Wypełnione tylko przez /api/ManagerUsers (null w managerach zagnieżdżonych w postach).
  final bool? hasLoginAccount;
  final bool? isAccessBlocked;

  /// E-mail logowania; wypełniony tylko przez /api/ManagerUsers (widok firmy).
  final String? email;

  String get fullName => '$firstName $lastName';

  factory ManagerUser.fromJson(Map<String, dynamic> json) => ManagerUser(
        id: json['id'] as String,
        firstName: json['firstName'] as String,
        lastName: json['lastName'] as String,
        age: (json['age'] as num).toInt(),
        description: json['description'] as String,
        socialAccountIds: ((json['socialAccountIds'] as List<dynamic>?) ?? const [])
            .map((e) => e as String)
            .toList(),
        hasLoginAccount: json['hasLoginAccount'] as bool?,
        isAccessBlocked: json['isAccessBlocked'] as bool?,
        email: json['email'] as String?,
      );
}

/// Odpowiada ManagerUserCreateDto (tworzenie: e-mail i hasło są wymagane)
/// oraz ManagerUserWriteDto (edycja danych osobowych - bez e-maila i hasła).
class ManagerInput {
  const ManagerInput({
    required this.firstName,
    required this.lastName,
    required this.age,
    required this.description,
    this.email,
    this.password,
  });

  final String firstName;
  final String lastName;
  final int age;
  final String description;
  final String? email;
  final String? password;

  Map<String, dynamic> toJson() => {
        'firstName': firstName,
        'lastName': lastName,
        'age': age,
        'description': description,
        if (email != null) 'email': email,
        if (password != null) 'password': password,
      };
}

/// Odpowiada ManagerCredentialsUpdateDto: nowy e-mail i opcjonalnie nowe hasło.
class ManagerCredentialsInput {
  const ManagerCredentialsInput({required this.email, this.password});

  final String email;
  final String? password;

  Map<String, dynamic> toJson() => {
        'email': email,
        if (password != null && password!.isNotEmpty) 'password': password,
      };
}

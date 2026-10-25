/// Odpowiada ManagerUserDto z backendu.
class ManagerUser {
  const ManagerUser({
    required this.id,
    required this.firstName,
    required this.lastName,
    required this.age,
    required this.description,
    this.socialAccountId,
    this.hasLoginAccount,
    this.isAccessBlocked,
  });

  final String id;
  final String firstName;
  final String lastName;
  final int age;
  final String description;
  final String? socialAccountId;

  /// Wypełnione tylko przez /api/ManagerUsers (null w managerach zagnieżdżonych w postach).
  final bool? hasLoginAccount;
  final bool? isAccessBlocked;

  String get fullName => '$firstName $lastName';

  factory ManagerUser.fromJson(Map<String, dynamic> json) => ManagerUser(
        id: json['id'] as String,
        firstName: json['firstName'] as String,
        lastName: json['lastName'] as String,
        age: (json['age'] as num).toInt(),
        description: json['description'] as String,
        socialAccountId: json['socialAccountId'] as String?,
        hasLoginAccount: json['hasLoginAccount'] as bool?,
        isAccessBlocked: json['isAccessBlocked'] as bool?,
      );
}

/// Odpowiada ManagerUserWriteDto (tworzenie i edycja).
class ManagerInput {
  const ManagerInput({
    required this.firstName,
    required this.lastName,
    required this.age,
    required this.description,
  });

  final String firstName;
  final String lastName;
  final int age;
  final String description;

  Map<String, dynamic> toJson() => {
        'firstName': firstName,
        'lastName': lastName,
        'age': age,
        'description': description,
      };
}

/// Odpowiada ClientUserDto (klient widziany przez firmę).
class ClientUser {
  const ClientUser({
    required this.id,
    required this.firstName,
    required this.lastName,
    required this.age,
    required this.description,
    required this.email,
  });

  final String id;
  final String firstName;
  final String lastName;
  final int age;
  final String description;
  final String email;

  String get fullName => '$firstName $lastName';

  factory ClientUser.fromJson(Map<String, dynamic> json) => ClientUser(
        id: json['id'] as String,
        firstName: json['firstName'] as String,
        lastName: json['lastName'] as String,
        age: (json['age'] as num).toInt(),
        description: json['description'] as String,
        email: json['email'] as String,
      );
}

/// Odpowiada CompanyDto (firma widziana przez powiązanego klienta).
class Company {
  const Company({required this.companyId, required this.name});

  final String companyId;
  final String name;

  factory Company.fromJson(Map<String, dynamic> json) => Company(
        companyId: json['companyId'] as String,
        name: json['name'] as String,
      );
}

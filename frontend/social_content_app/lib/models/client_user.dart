import 'enums.dart';

/// Odpowiada ClientUserDto (klient widziany przez firmę).
class ClientUser {
  const ClientUser({
    required this.id,
    required this.firstName,
    required this.lastName,
    required this.age,
    required this.description,
    required this.email,
    this.alias,
    this.displayName = '',
    this.pendingInvitations = 0,
  });

  final String id;
  final String firstName;
  final String lastName;
  final int age;
  final String description;
  final String email;

  /// Nazwa nadana klientowi przez firmę (null = brak) i nazwa do wyświetlania (alias albo imię i nazwisko).
  final String? alias;
  final String displayName;

  /// Liczba zaproszeń od tego klienta czekających na decyzję firmy.
  final int pendingInvitations;

  String get fullName => '$firstName $lastName';

  /// Nazwa kafelka: alias firmy, a gdy go nie ma - imię i nazwisko.
  String get title => displayName.isNotEmpty ? displayName : fullName;

  factory ClientUser.fromJson(Map<String, dynamic> json) => ClientUser(
        id: json['id'] as String,
        firstName: json['firstName'] as String,
        lastName: json['lastName'] as String,
        age: (json['age'] as num).toInt(),
        description: json['description'] as String,
        email: (json['email'] as String?) ?? '',
        alias: json['alias'] as String?,
        displayName: (json['displayName'] as String?) ?? '',
        pendingInvitations: (json['pendingInvitations'] as num?)?.toInt() ?? 0,
      );
}

/// Odpowiada CompanyDto (firma widziana przez powiązanego klienta).
class Company {
  const Company({required this.companyId, required this.name, this.approvalMode = PostApprovalMode.manager});

  final String companyId;
  final String name;

  /// Kto zatwierdza posty tej firmy na moich profilach.
  final PostApprovalMode approvalMode;

  factory Company.fromJson(Map<String, dynamic> json) => Company(
        companyId: json['companyId'] as String,
        name: json['name'] as String,
        approvalMode: PostApprovalMode.parse(json['approvalMode'] as String?),
      );
}

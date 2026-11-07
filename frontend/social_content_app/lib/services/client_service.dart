import '../models/client_user.dart';
import 'api_client.dart';

/// Relacja firma - klient (n - m).
class ClientService {
  ClientService(this._api);

  final ApiClient _api;

  /// CompanyUser: klienci powiązani z firmą.
  Future<List<ClientUser>> getForCompany() async {
    final json = await _api.get('/api/ClientUsers') as List<dynamic>;
    return json
        .map((e) => ClientUser.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  /// CompanyUser: powiązanie klienta (zarejestrowanego wcześniej) po adresie e-mail.
  Future<ClientUser> link(String email) async {
    final json = await _api.post('/api/ClientUsers', {'email': email});
    return ClientUser.fromJson(json as Map<String, dynamic>);
  }

  Future<void> unlink(String clientId) => _api.delete('/api/ClientUsers/$clientId');

  /// ClientUser: firmy, z którymi klient jest powiązany.
  Future<List<Company>> getMyCompanies() async {
    final json = await _api.get('/api/ClientUsers/me/companies') as List<dynamic>;
    return json.map((e) => Company.fromJson(e as Map<String, dynamic>)).toList();
  }
}

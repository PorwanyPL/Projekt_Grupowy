import '../models/social_account.dart';
import 'api_client.dart';

class SocialAccountService {
  SocialAccountService(this._api);

  final ApiClient _api;

  Future<List<SocialAccount>> getAll() async {
    final json = await _api.get('/api/SocialAccounts') as List<dynamic>;
    return json
        .map((e) => SocialAccount.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  Future<SocialAccount> create(SocialAccountInput input) async {
    final json = await _api.post('/api/SocialAccounts', input.toJson());
    return SocialAccount.fromJson(json as Map<String, dynamic>);
  }

  Future<SocialAccount> update(String id, SocialAccountInput input) async {
    final json = await _api.put('/api/SocialAccounts/$id', input.toJson());
    return SocialAccount.fromJson(json as Map<String, dynamic>);
  }

  /// CompanyUser: ustawia pełną listę managerów konta (pominięci zostają odpięci).
  Future<SocialAccount> assignManagers(String id, List<String> managerIds) async {
    final json = await _api.put('/api/SocialAccounts/$id/managers', {
      'managerUserIds': managerIds,
    });
    return SocialAccount.fromJson(json as Map<String, dynamic>);
  }

  Future<void> delete(String id) => _api.delete('/api/SocialAccounts/$id');

  /// ClientUser: zaprasza (inną) firmę do obsługi profilu.
  Future<SocialAccount> invite(String id, String companyEmail) async {
    final json = await _api.post('/api/SocialAccounts/$id/invite', {'companyEmail': companyEmail});
    return SocialAccount.fromJson(json as Map<String, dynamic>);
  }

  /// CompanyUser: przyjmuje lub odrzuca zaproszenie do obsługi profilu.
  Future<void> accept(String id) => _api.post('/api/SocialAccounts/$id/accept');

  Future<void> decline(String id) => _api.post('/api/SocialAccounts/$id/decline');
}

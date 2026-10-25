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

  Future<void> delete(String id) => _api.delete('/api/SocialAccounts/$id');
}

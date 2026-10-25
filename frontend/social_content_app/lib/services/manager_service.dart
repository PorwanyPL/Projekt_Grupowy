import '../models/manager_user.dart';
import 'api_client.dart';

class ManagerService {
  ManagerService(this._api);

  final ApiClient _api;

  Future<List<ManagerUser>> getAll() async {
    final json = await _api.get('/api/ManagerUsers') as List<dynamic>;
    return json
        .map((e) => ManagerUser.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  Future<ManagerUser> create(ManagerInput input) async {
    final json = await _api.post('/api/ManagerUsers', input.toJson());
    return ManagerUser.fromJson(json as Map<String, dynamic>);
  }

  Future<ManagerUser> update(String id, ManagerInput input) async {
    final json = await _api.put('/api/ManagerUsers/$id', input.toJson());
    return ManagerUser.fromJson(json as Map<String, dynamic>);
  }

  Future<void> delete(String id) => _api.delete('/api/ManagerUsers/$id');

  Future<ManagerUser> setAccessBlocked(String id, bool blocked) async {
    final json = await _api.put('/api/ManagerUsers/$id/access', {'blocked': blocked});
    return ManagerUser.fromJson(json as Map<String, dynamic>);
  }

  Future<ManagerUser> anonymize(String id) async {
    final json = await _api.post('/api/ManagerUsers/$id/anonymize');
    return ManagerUser.fromJson(json as Map<String, dynamic>);
  }
}

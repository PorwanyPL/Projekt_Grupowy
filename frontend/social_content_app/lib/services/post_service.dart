import '../models/enums.dart';
import '../models/post.dart';
import 'api_client.dart';

class PostService {
  PostService(this._api);

  final ApiClient _api;

  /// [accountId] - posty jednego konta; [managerId] - posty jednego managera
  /// (manager może podać tylko własny id, firma - dowolnego managera swojej firmy).
  Future<List<Post>> getAll({
    PostType? type,
    String? accountId,
    String? managerId,
  }) async {
    final query = {
      if (type != null) 'type': type.apiName,
      if (accountId != null) 'accountId': accountId,
      if (managerId != null) 'managerId': managerId,
    };
    final json = await _api.get(
      '/api/Posts',
      query: query.isEmpty ? null : query,
    ) as List<dynamic>;
    return json.map((e) => Post.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<Post> create(PostInput input) async {
    final json = await _api.post('/api/Posts', input.toJson());
    return Post.fromJson(json as Map<String, dynamic>);
  }

  Future<Post> update(String id, PostInput input) async {
    final json = await _api.put('/api/Posts/$id', input.toJson());
    return Post.fromJson(json as Map<String, dynamic>);
  }

  Future<void> delete(String id) => _api.delete('/api/Posts/$id');

  /// Zajmuje lub odnawia blokadę edycji; 409 = edytuje ktoś inny (komunikat z imieniem).
  Future<void> acquireLock(String id) => _api.post('/api/Posts/$id/lock');

  Future<void> releaseLock(String id) => _api.delete('/api/Posts/$id/lock');
}

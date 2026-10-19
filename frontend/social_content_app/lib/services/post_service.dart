import '../models/enums.dart';
import '../models/post.dart';
import 'api_client.dart';

class PostService {
  PostService(this._api);

  final ApiClient _api;

  Future<List<Post>> getAll({PostType? type}) async {
    final json = await _api.get(
      '/api/Posts',
      query: type == null ? null : {'type': type.apiName},
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
}

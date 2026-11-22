import '../models/enums.dart';
import '../models/post.dart';
import '../models/post_filter.dart';
import 'api_client.dart';

class PostService {
  PostService(this._api);

  final ApiClient _api;

  /// Posty widoczne dla roli, przefiltrowane przez [filter]. Post na kilka platform to jedna pozycja
  /// (pole [Post.targets] zawiera jego warianty). Pusty filtr = wszystko, co API udostępnia roli.
  Future<List<Post>> getAll([PostFilter filter = PostFilter.empty]) async {
    final query = filter.toQuery();
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

  /// ClientUser: zatwierdza lub odrzuca post czekający na akceptację.
  Future<Post> approve(String id) async =>
      Post.fromJson(await _api.post('/api/Posts/$id/approve') as Map<String, dynamic>);

  /// Odrzuca post; [comment] (opcjonalne uwagi z poprawkami, do 1000 znaków) trafia do autorów w powiadomieniu.
  Future<Post> reject(String id, {String? comment}) async {
    final text = comment?.trim();
    final body = text == null || text.isEmpty ? null : {'comment': text};
    return Post.fromJson(await _api.post('/api/Posts/$id/reject', body) as Map<String, dynamic>);
  }

  /// Pojedynczy post (np. po kliknięciu powiadomienia); 404, gdy nie istnieje albo nie jest widoczny dla roli.
  Future<Post> getById(String id) async =>
      Post.fromJson(await _api.get('/api/Posts/$id') as Map<String, dynamic>);
}

import 'package:flutter/foundation.dart';

import '../models/enums.dart';
import '../models/post.dart';
import '../models/post_filter.dart';
import '../services/post_service.dart';

/// Posty nie mają jednej globalnej listy: każdy widok (konto, manager, firma) pobiera własny
/// zakres przez [fetch]. Provider zapewnia też tworzenie, edycję i usuwanie.
class PostProvider extends ChangeNotifier {
  PostProvider(this._service);

  final PostService _service;

  /// Tagi widziane w pobranych postach - podpowiedzi w filtrach i formularzu (kalendarz zasila je przy każdym ładowaniu).
  final Set<String> knownTags = {};

  /// Rzuca [ApiException]. [filter] może zawierać także typ, konto i managera (zob. [PostFilter]).
  Future<List<Post>> fetch({
    PostType? type,
    String? accountId,
    String? managerId,
    PostFilter filter = PostFilter.empty,
  }) async {
    final merged = filter.copyWith(
      type: type,
      accountId: accountId,
      managerId: managerId,
    );
    final posts = await _service.getAll(merged);
    for (final p in posts) {
      knownTags.addAll(p.tags);
    }
    return posts;
  }

  /// Poniższe metody rzucają [ApiException] - formularz pokazuje komunikat.
  Future<Post> create(PostInput input) async {
    final post = await _service.create(input);
    notifyListeners();
    return post;
  }

  Future<Post> update(String id, PostInput input) async {
    final post = await _service.update(id, input);
    notifyListeners();
    return post;
  }

  /// Blokada edycji: rzuca [ApiException] (409), gdy post edytuje ktoś inny.
  Future<void> acquireLock(String id) => _service.acquireLock(id);

  Future<void> releaseLock(String id) async {
    try {
      await _service.releaseLock(id);
    } catch (_) {
      // blokada i tak wygaśnie sama
    }
  }

  Future<void> delete(String id) async {
    await _service.delete(id);
    notifyListeners();
  }

  Future<void> approve(String id) async {
    await _service.approve(id);
    notifyListeners();
  }

  Future<void> reject(String id, {String? comment}) async {
    await _service.reject(id, comment: comment);
    notifyListeners();
  }

  /// Rzuca [ApiException] (np. 404, gdy post został usunięty).
  Future<Post> getById(String id) => _service.getById(id);
}

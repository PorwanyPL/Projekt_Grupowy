import 'package:flutter/foundation.dart';

import '../models/enums.dart';
import '../models/post.dart';
import '../services/post_service.dart';

/// Posty nie mają jednej globalnej listy: każdy widok (konto, manager, firma) pobiera własny
/// zakres przez [fetch]. Provider zapewnia też tworzenie, edycję i usuwanie.
class PostProvider extends ChangeNotifier {
  PostProvider(this._service);

  final PostService _service;

  /// Rzuca [ApiException].
  Future<List<Post>> fetch({PostType? type, String? accountId, String? managerId}) =>
      _service.getAll(type: type, accountId: accountId, managerId: managerId);

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
}

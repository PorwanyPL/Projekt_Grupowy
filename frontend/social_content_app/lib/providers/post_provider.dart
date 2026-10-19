import 'package:flutter/foundation.dart';

import '../models/enums.dart';
import '../models/post.dart';
import '../services/api_exception.dart';
import '../services/post_service.dart';
import 'load_state.dart';

class PostProvider extends ChangeNotifier {
  PostProvider(this._service);

  final PostService _service;

  LoadState state = LoadState.idle;
  String? error;
  List<Post> items = const [];
  PostType? filter;

  Future<void> load() async {
    state = LoadState.loading;
    error = null;
    notifyListeners();
    try {
      items = await _service.getAll(type: filter);
      state = LoadState.loaded;
    } on ApiException catch (e) {
      error = e.message;
      state = LoadState.failed;
    }
    notifyListeners();
  }

  Future<void> setFilter(PostType? type) {
    filter = type;
    return load();
  }

  /// Poniższe metody rzucają [ApiException] - formularz pokazuje komunikat.
  Future<void> create(PostInput input) async {
    await _service.create(input);
    await load();
  }

  Future<void> update(String id, PostInput input) async {
    await _service.update(id, input);
    await load();
  }

  Future<void> delete(String id) async {
    await _service.delete(id);
    await load();
  }
}

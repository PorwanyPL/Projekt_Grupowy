import 'package:flutter/foundation.dart';

import '../models/manager_user.dart';
import '../services/api_exception.dart';
import '../services/manager_service.dart';
import 'load_state.dart';

class ManagerProvider extends ChangeNotifier {
  ManagerProvider(this._service);

  final ManagerService _service;

  LoadState state = LoadState.idle;
  String? error;
  List<ManagerUser> items = const [];

  Future<void> load() async {
    state = LoadState.loading;
    error = null;
    notifyListeners();
    try {
      items = await _service.getAll();
      state = LoadState.loaded;
    } on ApiException catch (e) {
      error = e.message;
      state = LoadState.failed;
    }
    notifyListeners();
  }

  /// Poniższe metody rzucają [ApiException] - formularz pokazuje komunikat.
  Future<void> create(ManagerInput input) async {
    await _service.create(input);
    await load();
  }

  Future<void> update(String id, ManagerInput input) async {
    await _service.update(id, input);
    await load();
  }

  /// Rzuca [ApiException] ze statusCode 409, gdy manager ma powiązane posty lub konta.
  Future<void> delete(String id) async {
    await _service.delete(id);
    await load();
  }

  Future<void> setAccessBlocked(String id, bool blocked) async {
    await _service.setAccessBlocked(id, blocked);
    await load();
  }

  Future<void> anonymize(String id) async {
    await _service.anonymize(id);
    await load();
  }
}

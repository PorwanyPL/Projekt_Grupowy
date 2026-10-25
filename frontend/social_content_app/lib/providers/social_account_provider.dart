import 'package:flutter/foundation.dart';

import '../models/social_account.dart';
import '../services/api_exception.dart';
import '../services/social_account_service.dart';
import 'load_state.dart';

class SocialAccountProvider extends ChangeNotifier {
  SocialAccountProvider(this._service);

  final SocialAccountService _service;

  LoadState state = LoadState.idle;
  String? error;
  List<SocialAccount> items = const [];

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
  Future<void> create(SocialAccountInput input) async {
    await _service.create(input);
    await load();
  }

  Future<void> update(String id, SocialAccountInput input) async {
    await _service.update(id, input);
    await load();
  }

  Future<void> delete(String id) async {
    await _service.delete(id);
    await load();
  }
}

import 'package:flutter/foundation.dart';

import '../models/client_user.dart';
import '../services/api_exception.dart';
import '../services/client_service.dart';
import 'load_state.dart';

/// Klienci firmy (widok CompanyUser) albo firmy klienta (widok ClientUser).
class ClientProvider extends ChangeNotifier {
  ClientProvider(this._service);

  final ClientService _service;

  LoadState state = LoadState.idle;
  String? error;
  List<ClientUser> clients = const [];
  List<Company> companies = const [];

  Future<void> loadClients() => _load(() async => clients = await _service.getForCompany());

  Future<void> loadCompanies() => _load(() async => companies = await _service.getMyCompanies());

  Future<void> _load(Future<void> Function() action) async {
    state = LoadState.loading;
    error = null;
    notifyListeners();
    try {
      await action();
      state = LoadState.loaded;
    } on ApiException catch (e) {
      error = e.message;
      state = LoadState.failed;
    }
    notifyListeners();
  }

  /// Rzucają [ApiException] - formularz pokazuje komunikat.
  Future<void> link(String email) async {
    await _service.link(email);
    await loadClients();
  }

  Future<void> unlink(String clientId) async {
    await _service.unlink(clientId);
    await loadClients();
  }
}

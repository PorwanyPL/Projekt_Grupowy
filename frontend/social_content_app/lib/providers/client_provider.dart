import 'package:flutter/foundation.dart';

import '../models/client_user.dart';
import '../models/enums.dart';
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

  /// Firma i manager dostają tę samą listę (serwer zawęża ją do klientów widocznych dla roli).
  Future<void> loadClients({bool silent = false}) =>
      _load(silent, () async => clients = await _service.getForCompany());

  Future<void> loadCompanies({bool silent = false}) =>
      _load(silent, () async => companies = await _service.getMyCompanies());

  /// [silent] - odświeżenie w tle (nawigacja, timer): bez spinnera, a przy błędzie zostają stare dane.
  Future<void> _load(bool silent, Future<void> Function() action) async {
    final keep = silent && state == LoadState.loaded;
    if (!keep) {
      state = LoadState.loading;
      error = null;
      notifyListeners();
    }
    try {
      await action();
      state = LoadState.loaded;
    } on ApiException catch (e) {
      if (!keep) {
        error = e.message;
        state = LoadState.failed;
      }
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

  Future<void> setAlias(String clientId, String? alias) async {
    await _service.setAlias(clientId, alias);
    await loadClients();
  }

  Future<void> setApprovalMode(String companyId, PostApprovalMode mode) async {
    await _service.setApprovalMode(companyId, mode);
    await loadCompanies(silent: true);
  }
}

import 'package:flutter/foundation.dart';

import '../models/auth.dart';
import '../services/api_client.dart';
import '../services/api_exception.dart';
import '../services/auth_service.dart';
import '../services/token_storage.dart';

enum AuthStatus { unknown, authenticated, unauthenticated }

/// Sesja użytkownika: logowanie, rejestracja, odtworzenie sesji po restarcie i wylogowanie.
/// Token trafia do [ApiClient], który dołącza go do każdego żądania.
class AuthProvider extends ChangeNotifier {
  AuthProvider(this._service, this._api, this._storage) {
    _api.onUnauthorized = _handleUnauthorized;
  }

  final AuthService _service;
  final ApiClient _api;
  final TokenStorage _storage;

  bool _handlingUnauthorized = false;

  AuthStatus status = AuthStatus.unknown;
  ManagerProfile? profile;

  /// Komunikat do pokazania na ekranie logowania (np. po wygaśnięciu sesji).
  String? notice;

  /// Wywoływane po utracie sesji (401 / wylogowanie) - UI zamyka otwarte ekrany.
  VoidCallback? onSessionEnded;

  /// Próbuje odtworzyć sesję zapisaną na urządzeniu.
  Future<void> restore() async {
    final saved = await _storage.read();
    if (saved == null || !saved.expiresAt.isAfter(DateTime.now())) {
      if (saved != null) await _storage.clear();
      _setUnauthenticated();
      return;
    }

    _api.setToken(saved.token);
    try {
      profile = await _service.me();
      status = AuthStatus.authenticated;
      notifyListeners();
    } on ApiException catch (e) {
      // 401 obsłużył już _handleUnauthorized; przy braku sieci zostawiamy zapisany token na później.
      if (e.statusCode != 401) {
        _api.setToken(null);
        notice = e.message;
        _setUnauthenticated();
      }
    }
  }

  /// Rzuca [ApiException] - formularz pokazuje komunikat.
  Future<void> login(String email, String password) async =>
      _startSession(await _service.login(email.trim(), password));

  Future<void> register(RegisterInput input) async =>
      _startSession(await _service.register(input));

  Future<void> logout() async {
    await _endSession();
    onSessionEnded?.call();
  }

  Future<void> _startSession(AuthSession session) async {
    _api.setToken(session.accessToken);
    await _storage.save(session.accessToken, session.expiresAt);
    profile = session.manager;
    notice = null;
    status = AuthStatus.authenticated;
    notifyListeners();
  }

  Future<void> _handleUnauthorized() async {
    if (_handlingUnauthorized) return; // kilka równoległych żądań może dostać 401 naraz
    _handlingUnauthorized = true;
    notice = 'Sesja wygasła lub dostęp został zablokowany. Zaloguj się ponownie.';
    await _endSession(keepNotice: true);
    _handlingUnauthorized = false;
    onSessionEnded?.call();
  }

  Future<void> _endSession({bool keepNotice = false}) async {
    _api.setToken(null);
    await _storage.clear();
    profile = null;
    if (!keepNotice) notice = null;
    _setUnauthenticated();
  }

  void _setUnauthenticated() {
    status = AuthStatus.unauthenticated;
    notifyListeners();
  }
}

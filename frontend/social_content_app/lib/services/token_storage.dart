import 'package:shared_preferences/shared_preferences.dart';

/// Przechowuje token i datę jego wygaśnięcia między uruchomieniami aplikacji.
/// Gdy zapis nie jest dostępny (np. tryb prywatny), sesja działa tylko w pamięci.
class TokenStorage {
  static const _tokenKey = 'auth_token';
  static const _expiresKey = 'auth_expires_at';

  Future<({String token, DateTime expiresAt})?> read() async {
    try {
      final prefs = await SharedPreferences.getInstance();
      final token = prefs.getString(_tokenKey);
      final expires = DateTime.tryParse(prefs.getString(_expiresKey) ?? '');
      if (token == null || expires == null) return null;
      return (token: token, expiresAt: expires);
    } catch (_) {
      return null;
    }
  }

  Future<void> save(String token, DateTime expiresAt) async {
    try {
      final prefs = await SharedPreferences.getInstance();
      await prefs.setString(_tokenKey, token);
      await prefs.setString(_expiresKey, expiresAt.toUtc().toIso8601String());
    } catch (_) {
      // brak trwałego zapisu - sesja zostaje w pamięci
    }
  }

  Future<void> clear() async {
    try {
      final prefs = await SharedPreferences.getInstance();
      await prefs.remove(_tokenKey);
      await prefs.remove(_expiresKey);
    } catch (_) {}
  }
}

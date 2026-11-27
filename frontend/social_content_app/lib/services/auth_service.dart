import '../models/auth.dart';
import 'api_client.dart';

class AuthService {
  AuthService(this._api);

  final ApiClient _api;

  Future<LoginResult> login(String email, String password) async {
    final json = await _api.post('/api/Auth/login', {
      'email': email,
      'password': password,
    });
    return LoginResult.fromJson(json as Map<String, dynamic>);
  }

  /// Drugi krok logowania: kod z e-maila.
  Future<AuthSession> verifyCode(String challengeId, String code) async {
    final json = await _api.post('/api/Auth/login/verify-code', {
      'challengeId': challengeId,
      'code': code.trim(),
    });
    return AuthSession.fromJson(json as Map<String, dynamic>);
  }

  /// Włącza/wyłącza weryfikację dwuetapową (wymaga hasła).
  Future<UserProfile> setTwoFactor(bool enabled, String password) async {
    final json = await _api.put('/api/Auth/two-factor', {
      'enabled': enabled,
      'password': password,
    });
    return UserProfile.fromJson(json as Map<String, dynamic>);
  }

  Future<AuthSession> registerCompany(RegisterCompanyInput input) async {
    final json = await _api.post('/api/Auth/register-company', input.toJson());
    return AuthSession.fromJson(json as Map<String, dynamic>);
  }

  Future<AuthSession> registerClient(RegisterClientInput input) async {
    final json = await _api.post('/api/Auth/register-client', input.toJson());
    return AuthSession.fromJson(json as Map<String, dynamic>);
  }

  Future<UserProfile> me() async {
    final json = await _api.get('/api/Auth/me');
    return UserProfile.fromJson(json as Map<String, dynamic>);
  }
}

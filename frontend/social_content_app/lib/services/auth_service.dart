import '../models/auth.dart';
import 'api_client.dart';

class AuthService {
  AuthService(this._api);

  final ApiClient _api;

  Future<AuthSession> login(String email, String password) async {
    final json = await _api.post('/api/Auth/login', {
      'email': email,
      'password': password,
    });
    return AuthSession.fromJson(json as Map<String, dynamic>);
  }

  Future<AuthSession> register(RegisterInput input) async {
    final json = await _api.post('/api/Auth/register', input.toJson());
    return AuthSession.fromJson(json as Map<String, dynamic>);
  }

  Future<ManagerProfile> me() async {
    final json = await _api.get('/api/Auth/me');
    return ManagerProfile.fromJson(json as Map<String, dynamic>);
  }
}

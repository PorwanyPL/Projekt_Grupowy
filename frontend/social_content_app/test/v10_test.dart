import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:provider/provider.dart';
import 'package:social_content_app/config/api_config.dart';
import 'package:social_content_app/models/auth.dart';
import 'package:social_content_app/models/chat.dart';
import 'package:social_content_app/providers/auth_provider.dart';
import 'package:social_content_app/screens/login_screen.dart';
import 'package:social_content_app/screens/profile_screen.dart';
import 'package:social_content_app/services/api_client.dart';
import 'package:social_content_app/services/auth_service.dart';
import 'package:social_content_app/services/token_storage.dart';

http.Response _json(Object body, [int status = 200]) =>
    http.Response(jsonEncode(body), status, headers: {'content-type': 'application/json; charset=utf-8'});

Map<String, dynamic> _user({bool twoFactor = false}) => {
      'id': 'u1',
      'role': 'ClientUser',
      'email': 'klient3@demo.pl',
      'displayName': 'Marta Obca',
      'companyId': null,
      'age': 28,
      'description': 'Opis',
      'twoFactorEnabled': twoFactor,
    };

Map<String, dynamic> _session() => {
      'accessToken': 'jwt-token',
      'tokenType': 'Bearer',
      'expiresAt': '2099-01-01T00:00:00Z',
      'user': _user(twoFactor: true),
    };

Map<String, dynamic> _challenge() => {
      'accessToken': '',
      'tokenType': 'Bearer',
      'expiresAt': '0001-01-01T00:00:00',
      'user': null,
      'twoFactorRequired': true,
      'challengeId': 'ch-1',
      'maskedEmail': 'k***@demo.pl',
    };

class _MemoryStorage extends TokenStorage {
  @override
  Future<({String token, DateTime expiresAt})?> read() async => null;
  @override
  Future<void> save(String token, DateTime expiresAt) async {}
  @override
  Future<void> clear() async {}
}

AuthProvider _provider(MockClient client) {
  final api = ApiClient(client: client, baseUrl: 'http://x');
  return AuthProvider(AuthService(api), api, _MemoryStorage());
}

Widget _app(AuthProvider auth, Widget home) => ChangeNotifierProvider<AuthProvider>.value(
      value: auth,
      child: MaterialApp(home: home),
    );

void main() {
  group('Wyszukiwanie rozmówcy (v10)', () {
    test('zostały tylko tryby: fragment nazwy i e-mail', () {
      expect(ContactSearchMode.values.map((m) => m.apiName), ['Contains', 'Email']);
    });
  });

  group('Modele i serwis 2FA', () {
    test('UserProfile czyta twoFactorEnabled (brak pola = false)', () {
      expect(UserProfile.fromJson(_user(twoFactor: true)).twoFactorEnabled, isTrue);
      expect(UserProfile.fromJson(_user()..remove('twoFactorEnabled')).twoFactorEnabled, isFalse);
    });

    test('LoginResult rozróżnia sesję i wyzwanie', () {
      final challenge = LoginResult.fromJson(_challenge());
      expect(challenge.session, isNull);
      expect(challenge.challenge!.challengeId, 'ch-1');
      expect(challenge.challenge!.maskedEmail, 'k***@demo.pl');

      final session = LoginResult.fromJson(_session());
      expect(session.challenge, isNull);
      expect(session.session!.accessToken, 'jwt-token');
    });

    test('verifyCode i setTwoFactor wołają właściwe endpointy', () async {
      final calls = <String>[];
      final bodies = <Object?>[];
      final client = MockClient((req) async {
        calls.add('${req.method} ${req.url.path}');
        bodies.add(jsonDecode(req.body));
        return req.url.path.endsWith('two-factor') ? _json(_user(twoFactor: true)) : _json(_session());
      });
      final service = AuthService(ApiClient(client: client, baseUrl: 'http://x'));

      final session = await service.verifyCode('ch-1', ' 123456 ');
      final profile = await service.setTwoFactor(true, 'Haslo1234');

      expect(session.user.email, 'klient3@demo.pl');
      expect(profile.twoFactorEnabled, isTrue);
      expect(calls, ['POST /api/Auth/login/verify-code', 'PUT /api/Auth/two-factor']);
      expect(bodies[0], {'challengeId': 'ch-1', 'code': '123456'});
      expect(bodies[1], {'enabled': true, 'password': 'Haslo1234'});
    });
  });

  group('Ekran logowania z kodem', () {
    testWidgets('po haśle pokazuje pole na kod, a poprawny kod loguje', (tester) async {
      final client = MockClient((req) async {
        if (req.url.path == '/api/Auth/login') return _json(_challenge());
        if (req.url.path == '/api/Auth/login/verify-code') {
          final body = jsonDecode(req.body) as Map<String, dynamic>;
          return body['code'] == '123456'
              ? _json(_session())
              : _json({'title': 'Unauthorized', 'status': 401, 'detail': 'Nieprawidłowy kod. Pozostało prób: 4.'}, 401);
        }
        return _json({}, 404);
      });
      final auth = _provider(client);
      await tester.pumpWidget(_app(auth, const LoginScreen()));

      await tester.enterText(find.byType(TextFormField).at(0), 'klient3@demo.pl');
      await tester.enterText(find.byType(TextFormField).at(1), 'ToJestHaslo1!');
      await tester.tap(find.text('Zaloguj się'));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('two-factor-code')), findsOneWidget);
      expect(find.textContaining('k***@demo.pl'), findsOneWidget);
      expect(auth.status, isNot(AuthStatus.authenticated));

      // Zły format nie idzie do serwera.
      await tester.enterText(find.byKey(const Key('two-factor-code')), '123');
      await tester.tap(find.byKey(const Key('two-factor-submit')));
      await tester.pump();
      expect(find.text('Kod składa się z 6 cyfr.'), findsOneWidget);

      // Zły kod: komunikat z serwera.
      await tester.enterText(find.byKey(const Key('two-factor-code')), '000000');
      await tester.tap(find.byKey(const Key('two-factor-submit')));
      await tester.pumpAndSettle();
      expect(find.textContaining('Pozostało prób: 4'), findsOneWidget);
      expect(auth.status, isNot(AuthStatus.authenticated));

      // Dobry kod: sesja.
      await tester.enterText(find.byKey(const Key('two-factor-code')), '123456');
      await tester.tap(find.byKey(const Key('two-factor-submit')));
      // Bez AuthGate ekran zostaje z wskaźnikiem ładowania (animacja bez końca), więc pumpAndSettle by nie wrócił.
      await tester.pump(const Duration(milliseconds: 100));
      await tester.pump(const Duration(milliseconds: 100));
      expect(auth.status, AuthStatus.authenticated);
      expect(auth.profile!.twoFactorEnabled, isTrue);
    });

    testWidgets('Wróć przywraca formularz hasła', (tester) async {
      final client = MockClient((_) async => _json(_challenge()));
      await tester.pumpWidget(_app(_provider(client), const LoginScreen()));
      await tester.enterText(find.byType(TextFormField).at(0), 'klient3@demo.pl');
      await tester.enterText(find.byType(TextFormField).at(1), 'ToJestHaslo1!');
      await tester.tap(find.text('Zaloguj się'));
      await tester.pumpAndSettle();

      await tester.tap(find.textContaining('Wróć'));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('two-factor-code')), findsNothing);
      expect(find.text('Zaloguj się'), findsOneWidget);
    });
  });

  group('Profil: przełącznik 2FA', () {
    testWidgets('włączenie wymaga hasła i wysyła PUT', (tester) async {
      Map<String, dynamic>? sent;
      final client = MockClient((req) async {
        if (req.method == 'PUT') {
          sent = jsonDecode(req.body) as Map<String, dynamic>;
          return _json(_user(twoFactor: true));
        }
        return _json({}, 404);
      });
      final auth = _provider(client)..profile = UserProfile.fromJson(_user());
      await tester.pumpWidget(_app(auth, const ProfileScreen()));

      final tile = find.byKey(const Key('two-factor-switch'));
      expect(tester.widget<SwitchListTile>(tile).value, isFalse);

      await tester.tap(tile);
      await tester.pumpAndSettle();
      await tester.enterText(find.byKey(const Key('two-factor-password')), 'ToJestHaslo1!');
      await tester.tap(find.byKey(const Key('two-factor-confirm')));
      await tester.pumpAndSettle();

      expect(sent, {'enabled': true, 'password': 'ToJestHaslo1!'});
      expect(tester.widget<SwitchListTile>(tile).value, isTrue);
    });

    testWidgets('Anuluj nic nie wysyła', (tester) async {
      var calls = 0;
      final client = MockClient((_) async {
        calls++;
        return _json({}, 404);
      });
      final auth = _provider(client)..profile = UserProfile.fromJson(_user());
      await tester.pumpWidget(_app(auth, const ProfileScreen()));

      await tester.tap(find.byKey(const Key('two-factor-switch')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Anuluj'));
      await tester.pumpAndSettle();

      expect(calls, 0);
      expect(auth.profile!.twoFactorEnabled, isFalse);
    });
  });

  group('HTTPS (audyt transportu)', () {
    test('HTTPS i pętla zwrotna są dozwolone, zdalny HTTP - nie', () {
      expect(ApiConfig.isSecureOrLoopback('https://api.example.com'), isTrue);
      expect(ApiConfig.isSecureOrLoopback('http://127.0.0.1:5080'), isTrue);
      expect(ApiConfig.isSecureOrLoopback('http://localhost:5080'), isTrue);
      expect(ApiConfig.isSecureOrLoopback('http://10.0.2.2:5080'), isTrue);
      expect(ApiConfig.isSecureOrLoopback('http://192.168.1.20:5080'), isFalse);
      expect(ApiConfig.isSecureOrLoopback('http://api.example.com'), isFalse);
      expect(ApiConfig.isSecureOrLoopback('nie-adres'), isFalse);
    });
  });
}

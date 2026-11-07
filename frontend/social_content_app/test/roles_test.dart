import 'dart:convert';
import 'dart:typed_data';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:social_content_app/models/auth.dart';
import 'package:social_content_app/models/manager_user.dart';
import 'package:social_content_app/providers/client_provider.dart';
import 'package:social_content_app/services/api_client.dart';
import 'package:social_content_app/services/api_exception.dart';
import 'package:social_content_app/services/auth_service.dart';
import 'package:social_content_app/services/client_service.dart';
import 'package:social_content_app/services/media_service.dart';
import 'package:social_content_app/services/manager_service.dart';
import 'package:social_content_app/services/notification_service.dart';
import 'package:social_content_app/services/post_service.dart';
import 'package:social_content_app/services/social_account_service.dart';

http.Response _json(Object body, [int status = 200]) => http.Response(
      jsonEncode(body),
      status,
      headers: {'content-type': 'application/json; charset=utf-8'},
    );

Map<String, dynamic> _session(String role) => {
      'accessToken': 'tok',
      'tokenType': 'Bearer',
      'expiresAt': DateTime.now().add(const Duration(hours: 1)).toUtc().toIso8601String(),
      'user': {
        'id': 'u1',
        'role': role,
        'email': 'a@b.pl',
        'displayName': 'Nazwa',
        'companyId': null,
        'age': null,
        'description': null,
      },
    };

void main() {
  group('role', () {
    test('UserRole.parse rozpoznaje nazwy z backendu', () {
      expect(UserRole.parse('CompanyUser'), UserRole.companyUser);
      expect(UserRole.parse('ManagerUser'), UserRole.managerUser);
      expect(UserRole.parse('ClientUser'), UserRole.clientUser);
      expect(() => UserRole.parse('Root'), throwsFormatException);
    });

    test('AuthSession.fromJson czyta profil z rolą', () {
      final session = AuthSession.fromJson(_session('ClientUser'));
      expect(session.user.role, UserRole.clientUser);
      expect(session.user.isClient, isTrue);
      expect(session.user.isCompany, isFalse);
    });
  });

  group('rejestracja', () {
    test('firma i klient używają osobnych endpointów', () async {
      final paths = <String>[];
      final api = ApiClient(
        baseUrl: 'http://test',
        client: MockClient((req) async {
          paths.add(req.url.path);
          return _json(_session(req.url.path.endsWith('company') ? 'CompanyUser' : 'ClientUser'), 201);
        }),
      );
      final service = AuthService(api);

      final company = await service.registerCompany(
          const RegisterCompanyInput(name: 'ACME', email: 'a@b.pl', password: 'Haslo1234'));
      final client = await service.registerClient(const RegisterClientInput(
        firstName: 'Karol',
        lastName: 'Klient',
        age: 30,
        description: 'Opis',
        email: 'k@b.pl',
        password: 'Haslo1234',
      ));

      expect(paths, ['/api/Auth/register-company', '/api/Auth/register-client']);
      expect(company.user.role, UserRole.companyUser);
      expect(client.user.role, UserRole.clientUser);
    });
  });

  group('managerowie i konta', () {
    test('ManagerInput wysyła e-mail i hasło tylko gdy podane', () {
      const without = ManagerInput(firstName: 'A', lastName: 'B', age: 30, description: 'd');
      expect(without.toJson().containsKey('email'), isFalse);
      expect(without.toJson().containsKey('password'), isFalse);

      const withLogin = ManagerInput(
          firstName: 'A', lastName: 'B', age: 30, description: 'd', email: 'a@b.pl', password: 'Haslo1234');
      expect(withLogin.toJson()['email'], 'a@b.pl');
      expect(withLogin.toJson()['password'], 'Haslo1234');
    });

    test('ManagerUser.fromJson czyta listę przypisanych kont', () {
      final m = ManagerUser.fromJson({
        'id': '1', 'firstName': 'A', 'lastName': 'B', 'age': 30, 'description': 'd',
        'socialAccountIds': ['x', 'y'],
      });
      expect(m.socialAccountIds, ['x', 'y']);
    });

    test('przypisanie managerów wysyła PUT /managers z pełną listą', () async {
      late http.Request request;
      final api = ApiClient(
        baseUrl: 'http://test',
        client: MockClient((req) async {
          request = req;
          return _json({
            'id': 'a1', 'accountId': 'acc', 'platform': 'Facebook', 'externalPlatformId': 'e',
            'accountName': 'Konto', 'tokenExpiresAt': DateTime.now().toUtc().toIso8601String(),
            'isActive': true, 'errorMessage': null, 'hasRefreshToken': false,
            'managerUsers': [
              {'id': 'm1', 'firstName': 'A', 'lastName': 'B', 'age': 30, 'description': 'd'}
            ],
          });
        }),
      );

      final account = await SocialAccountService(api).assignManagers('a1', ['m1']);

      expect(request.method, 'PUT');
      expect(request.url.path, '/api/SocialAccounts/a1/managers');
      expect(jsonDecode(request.body), {'managerUserIds': ['m1']});
      expect(account.hasManager('m1'), isTrue);
      expect(account.hasManager('m2'), isFalse);
    });
  });

  group('posty', () {
    test('filtry konta i managera trafiają do query string', () async {
      late Uri uri;
      final api = ApiClient(
        baseUrl: 'http://test',
        client: MockClient((req) async {
          uri = req.url;
          return _json([]);
        }),
      );

      await PostService(api).getAll(accountId: 'a1', managerId: 'm1');

      expect(uri.path, '/api/Posts');
      expect(uri.queryParameters, {'accountId': 'a1', 'managerId': 'm1'});
    });

    test('blokada edycji: zajęcie i zwolnienie używają endpointów /lock', () async {
      final calls = <String>[];
      final api = ApiClient(
        baseUrl: 'http://test',
        client: MockClient((req) async {
          calls.add('${req.method} ${req.url.path}');
          return req.method == 'DELETE' ? http.Response('', 204) : _json({});
        }),
      );
      final service = PostService(api);
      await service.acquireLock('p1');
      await service.releaseLock('p1');
      expect(calls, ['POST /api/Posts/p1/lock', 'DELETE /api/Posts/p1/lock']);
    });

    test('409 przy blokadzie zwraca komunikat serwera z imieniem edytującego', () async {
      final api = ApiClient(
        baseUrl: 'http://test',
        client: MockClient((req) async => _json({
              'title': 'Konflikt',
              'detail': 'Ten post jest właśnie edytowany przez: Anna Nowak.'
            }, 409)),
      );
      await expectLater(
        PostService(api).acquireLock('p1'),
        throwsA(isA<ApiException>()
            .having((e) => e.statusCode, 'status', 409)
            .having((e) => e.message, 'message', contains('Anna Nowak'))),
      );
    });
  });

  group('media i powiadomienia', () {
    test('upload wysyła multipart na /api/Media i czyta MediaUploadDto', () async {
      late http.BaseRequest sent;
      final api = ApiClient(
        baseUrl: 'http://test',
        client: MockClient((req) async {
          sent = req;
          return _json({
            'storageKey': 'abc.png', 'fileName': 'foto.png', 'kind': 'Picture',
            'format': 'Png', 'sizeKb': 12
          });
        }),
      );
      api.setToken('tok');
      final media = await MediaService(api).upload(Uint8List.fromList([1, 2, 3]), 'foto.png');

      expect(sent.method, 'POST');
      expect(sent.url.path, '/api/Media');
      expect(sent.headers['Authorization'], 'Bearer tok');
      expect(sent.headers['content-type'], startsWith('multipart/form-data'));
      expect(media.storageKey, 'abc.png');
      expect(media.sizeKb, 12);
      expect(MediaService(api).urlFor('abc.png'), 'http://test/api/Media/abc.png');
    });

    test('powiadomienia: lista i oznaczenie jako przeczytane', () async {
      final calls = <String>[];
      final api = ApiClient(
        baseUrl: 'http://test',
        client: MockClient((req) async {
          calls.add('${req.method} ${req.url.path}');
          if (req.method == 'PUT') return http.Response('', 204);
          return _json([
            {'id': 'n1', 'postId': 'p1', 'message': 'Błąd publikacji', 'createdAt': '2030-01-01T10:00:00Z', 'isRead': false}
          ]);
        }),
      );
      final service = NotificationService(api);
      final items = await service.getAll();
      await service.markAllRead();
      expect(items.single.isRead, isFalse);
      expect(calls, ['GET /api/Notifications', 'PUT /api/Notifications/read-all']);
    });

    test('zmiana danych logowania managera idzie na /credentials, puste hasło jest pomijane', () async {
      late http.Request sent;
      final api = ApiClient(
        baseUrl: 'http://test',
        client: MockClient((req) async {
          sent = req;
          return _json({
            'id': 'm1', 'firstName': 'A', 'lastName': 'B', 'age': 30, 'description': 'd', 'email': 'nowy@b.pl'
          });
        }),
      );
      final m = await ManagerService(api)
          .updateCredentials('m1', const ManagerCredentialsInput(email: 'nowy@b.pl', password: ''));
      expect(sent.url.path, '/api/ManagerUsers/m1/credentials');
      expect(jsonDecode(sent.body), {'email': 'nowy@b.pl'});
      expect(m.email, 'nowy@b.pl');
    });
  });

  group('klienci', () {
    test('powiązanie i odpięcie klienta odświeżają listę firmy', () async {
      final calls = <String>[];
      final api = ApiClient(
        baseUrl: 'http://test',
        client: MockClient((req) async {
          calls.add('${req.method} ${req.url.path}');
          if (req.method == 'POST') {
            return _json({
              'id': 'c1', 'firstName': 'K', 'lastName': 'L', 'age': 30, 'description': 'd', 'email': 'k@b.pl'
            }, 201);
          }
          if (req.method == 'DELETE') return http.Response('', 204);
          return _json([]);
        }),
      );
      final provider = ClientProvider(ClientService(api));

      await provider.link('k@b.pl');
      await provider.unlink('c1');

      expect(calls, [
        'POST /api/ClientUsers',
        'GET /api/ClientUsers',
        'DELETE /api/ClientUsers/c1',
        'GET /api/ClientUsers',
      ]);
    });

    test('klient pobiera swoje firmy z /me/companies', () async {
      late String path;
      final api = ApiClient(
        baseUrl: 'http://test',
        client: MockClient((req) async {
          path = req.url.path;
          return _json([
            {'companyId': 'f1', 'name': 'Firma 1'},
            {'companyId': 'f2', 'name': 'Firma 2'},
          ]);
        }),
      );
      final provider = ClientProvider(ClientService(api));

      await provider.loadCompanies();

      expect(path, '/api/ClientUsers/me/companies');
      expect(provider.companies.map((c) => c.name), ['Firma 1', 'Firma 2']);
    });
  });
}

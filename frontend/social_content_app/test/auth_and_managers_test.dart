import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:social_content_app/models/manager_user.dart';
import 'package:social_content_app/providers/manager_provider.dart';
import 'package:social_content_app/services/api_client.dart';
import 'package:social_content_app/services/api_exception.dart';
import 'package:social_content_app/services/manager_service.dart';

http.Response _json(Object body, [int status = 200]) => http.Response(
      jsonEncode(body),
      status,
      headers: {'content-type': 'application/json; charset=utf-8'},
    );

Map<String, dynamic> _manager(String id, {bool login = true, bool blocked = false}) => {
      'id': id,
      'firstName': 'Anna',
      'lastName': 'Nowak',
      'age': 30,
      'description': 'Opis',
      'socialAccountId': null,
      'hasLoginAccount': login,
      'isAccessBlocked': blocked,
    };

void main() {
  group('ApiClient i sesja', () {
    test('dołącza token Bearer do każdego żądania', () async {
      String? header;
      final api = ApiClient(
        baseUrl: 'http://test',
        client: MockClient((req) async {
          header = req.headers['Authorization'];
          return _json([]);
        }),
      )..setToken('abc');

      await api.get('/api/Posts');
      expect(header, 'Bearer abc');
    });

    test('401 przy aktywnym tokenie wywołuje onUnauthorized', () async {
      var called = 0;
      final api = ApiClient(
        baseUrl: 'http://test',
        client: MockClient((_) async => http.Response('', 401)),
      )
        ..setToken('abc')
        ..onUnauthorized = () => called++;

      await expectLater(
        api.get('/api/Posts'),
        throwsA(isA<ApiException>().having((e) => e.statusCode, 'status', 401)),
      );
      expect(called, 1);
    });

    test('401 przy logowaniu (bez tokenu) jest zwykłym błędem z komunikatem API', () async {
      var called = 0;
      final api = ApiClient(
        baseUrl: 'http://test',
        client: MockClient((_) async => _json(
              {'status': 401, 'title': 'Brak autoryzacji', 'detail': 'Nieprawidłowy adres e-mail lub hasło.'},
              401,
            )),
      )..onUnauthorized = () => called++;

      await expectLater(
        api.post('/api/Auth/login', {'email': 'a@b.pl', 'password': 'x'}),
        throwsA(isA<ApiException>()
            .having((e) => e.message, 'message', 'Nieprawidłowy adres e-mail lub hasło.')),
      );
      expect(called, 0);
    });

    test('błędy walidacji z API są łączone w czytelny komunikat', () async {
      final api = ApiClient(
        baseUrl: 'http://test',
        client: MockClient((_) async => _json({
              'errors': {
                'Email': ['Niepoprawny e-mail'],
                'Password': ['Za krótkie']
              }
            }, 400)),
      );

      await expectLater(
        api.post('/api/Auth/register', {}),
        throwsA(isA<ApiException>()
            .having((e) => e.message, 'message', 'Niepoprawny e-mail\nZa krótkie')),
      );
    });
  });

  group('ManagerProvider', () {
    late List<http.Request> requests;
    late ManagerProvider provider;
    late int deleteStatus;

    setUp(() {
      requests = [];
      deleteStatus = 204;
      final api = ApiClient(
        baseUrl: 'http://test',
        client: MockClient((req) async {
          requests.add(req);
          final path = req.url.path;
          if (req.method == 'GET') return _json([_manager('1')]);
          if (req.method == 'DELETE') {
            return deleteStatus == 204
                ? http.Response('', 204)
                : _json({
                    'status': 409,
                    'detail': 'Nie można usunąć managera - zablokuj dostęp zamiast usuwać.'
                  }, 409);
          }
          if (req.method == 'PUT' && path.endsWith('/access')) {
            return _json(_manager('1', blocked: true));
          }
          if (req.method == 'POST' && path.endsWith('/anonymize')) {
            return _json({..._manager('1', login: false), 'firstName': 'Usunięty', 'lastName': 'użytkownik'});
          }
          return http.Response('', 404);
        }),
      );
      provider = ManagerProvider(ManagerService(api));
    });

    test('ManagerUser.fromJson czyta flagi konta (i toleruje ich brak)', () {
      final full = ManagerUser.fromJson(_manager('1', blocked: true));
      expect(full.hasLoginAccount, isTrue);
      expect(full.isAccessBlocked, isTrue);

      final nested = ManagerUser.fromJson({
        'id': '2', 'firstName': 'B', 'lastName': 'C', 'age': 20, 'description': 'd',
      });
      expect(nested.hasLoginAccount, isNull);
      expect(nested.isAccessBlocked, isNull);
    });

    test('usunięcie bez powiązań (204) odświeża listę', () async {
      await provider.delete('1');
      expect(requests.map((r) => r.method), ['DELETE', 'GET']);
    });

    test('usunięcie z powiązaniami zwraca ApiException 409 z komunikatem API', () async {
      deleteStatus = 409;
      await expectLater(
        provider.delete('1'),
        throwsA(isA<ApiException>()
            .having((e) => e.statusCode, 'status', 409)
            .having((e) => e.message, 'message', contains('zablokuj dostęp zamiast usuwać'))),
      );
      expect(requests.map((r) => r.method), ['DELETE']); // lista nie jest przeładowywana
    });

    test('blokada wysyła PUT /access z polem blocked', () async {
      await provider.setAccessBlocked('1', true);
      final put = requests.firstWhere((r) => r.method == 'PUT');
      expect(put.url.path, '/api/ManagerUsers/1/access');
      expect(jsonDecode(put.body), {'blocked': true});
    });

    test('anonimizacja wysyła POST /anonymize i odświeża listę', () async {
      await provider.anonymize('1');
      final post = requests.firstWhere((r) => r.method == 'POST');
      expect(post.url.path, '/api/ManagerUsers/1/anonymize');
      expect(requests.last.method, 'GET');
    });
  });
}

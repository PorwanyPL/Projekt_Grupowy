import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:social_content_app/models/client_user.dart';
import 'package:social_content_app/models/enums.dart';
import 'package:social_content_app/models/post.dart';
import 'package:social_content_app/models/social_account.dart';
import 'package:social_content_app/services/api_client.dart';
import 'package:social_content_app/services/client_service.dart';
import 'package:social_content_app/services/post_service.dart';
import 'package:social_content_app/services/social_account_service.dart';
import 'package:social_content_app/widgets/app_messenger.dart';

http.Response _json(Object? body, [int status = 200]) => http.Response(
      jsonEncode(body),
      status,
      headers: {'content-type': 'application/json; charset=utf-8'},
    );

Map<String, dynamic> _account({String status = 'Accepted'}) => {
      'id': 'a1', 'accountId': 'acc', 'platform': 'Facebook', 'externalPlatformId': 'e',
      'accountName': 'Konto', 'tokenExpiresAt': '2030-01-01T00:00:00Z', 'isActive': true,
      'errorMessage': null, 'hasRefreshToken': false, 'managerUsers': [],
      'ownerClientId': 'c1', 'ownerName': 'Kowalski (alias)', 'companyId': 'f1',
      'companyName': 'Firma', 'managementStatus': status, 'approvalMode': 'Client',
    };

void main() {
  group('modele v6', () {
    test('SocialAccount czyta właściciela, firmę, zaproszenie i tryb zatwierdzania', () {
      final a = SocialAccount.fromJson(_account(status: 'Invited'));
      expect(a.ownerClientId, 'c1');
      expect(a.ownerName, 'Kowalski (alias)');
      expect(a.companyName, 'Firma');
      expect(a.isInvited, isTrue);
      expect(a.isAccepted, isFalse);
      expect(a.approvalMode, PostApprovalMode.client);
    });

    test('SocialAccount bez nowych pól (stara odpowiedź) ma bezpieczne wartości domyślne', () {
      final json = _account()..removeWhere((k, _) => ['ownerClientId', 'companyId', 'managementStatus', 'approvalMode'].contains(k));
      final a = SocialAccount.fromJson(json);
      expect(a.approvalMode, PostApprovalMode.manager);
      expect(a.managementStatus, ManagementStatus.invited);
    });

    test('SocialAccountInput wysyła companyEmail zamiast managerów', () {
      final json = SocialAccountInput(
        accountId: 'x', platform: SocialPlatform.tikTok, externalPlatformId: 'e', accountName: 'n',
        tokenExpiresAt: DateTime(2030), isActive: true, companyEmail: 'firma@x.pl',
      ).toJson();
      expect(json['companyEmail'], 'firma@x.pl');
      expect(json.containsKey('managerUserIds'), isFalse);
    });

    test('ClientUser: alias ma pierwszeństwo przed imieniem i nazwiskiem', () {
      final c = ClientUser.fromJson({
        'id': 'c1', 'firstName': 'Jan', 'lastName': 'Kowalski', 'age': 30, 'description': 'd',
        'email': '', 'alias': 'Sklep Janka', 'displayName': 'Sklep Janka', 'pendingInvitations': 2,
      });
      expect(c.title, 'Sklep Janka');
      expect(c.pendingInvitations, 2);
      final noAlias = ClientUser.fromJson({
        'id': 'c1', 'firstName': 'Jan', 'lastName': 'Kowalski', 'age': 30, 'description': 'd', 'email': 'a@b.pl'});
      expect(noAlias.title, 'Jan Kowalski');
    });

    test('Company czyta tryb zatwierdzania', () {
      final c = Company.fromJson({'companyId': 'f1', 'name': 'Firma', 'approvalMode': 'Client'});
      expect(c.approvalMode, PostApprovalMode.client);
    });

    test('Post: nowe statusy i flaga canApprove', () {
      final post = Post.fromJson({
        'id': 'p1', 'title': 'T', 'createdAt': '2030-01-01T10:00:00Z', 'description': 'D',
        'visibility': 'Text', 'pictures': [], 'videos': [], 'managerUsers': [],
        'status': 'AwaitingApproval', 'canEdit': false, 'canDelete': false, 'canApprove': true,
      });
      expect(post.status, PostStatus.awaitingApproval);
      expect(post.canApprove, isTrue);
      expect(PostStatus.parse('Rejected'), PostStatus.rejected);
    });
  });

  group('serwisy v6', () {
    test('zaproszenie, przyjęcie i odrzucenie używają właściwych endpointów', () async {
      final calls = <String>[];
      final api = ApiClient(
        baseUrl: 'http://test',
        client: MockClient((req) async {
          calls.add('${req.method} ${req.url.path}');
          return req.url.path.endsWith('/invite') ? _json(_account(status: 'Invited')) : _json(null, 204);
        }),
      );
      final service = SocialAccountService(api);
      await service.invite('a1', 'firma@x.pl');
      await service.accept('a1');
      await service.decline('a1');
      expect(calls, [
        'POST /api/SocialAccounts/a1/invite',
        'POST /api/SocialAccounts/a1/accept',
        'POST /api/SocialAccounts/a1/decline',
      ]);
    });

    test('alias i tryb zatwierdzania: PUT z oczekiwanymi ciałami', () async {
      final requests = <http.Request>[];
      final api = ApiClient(
        baseUrl: 'http://test',
        client: MockClient((req) async {
          requests.add(req);
          return _json(null, 204);
        }),
      );
      final service = ClientService(api);
      await service.setAlias('c1', 'Sklep');
      await service.setApprovalMode('f1', PostApprovalMode.client);
      expect(requests[0].url.path, '/api/ClientUsers/c1/alias');
      expect(jsonDecode(requests[0].body), {'alias': 'Sklep'});
      expect(requests[1].url.path, '/api/ClientUsers/me/companies/f1/approval');
      expect(jsonDecode(requests[1].body), {'mode': 'Client'});
    });

    test('zatwierdzanie i odrzucanie posta', () async {
      final calls = <String>[];
      final api = ApiClient(
        baseUrl: 'http://test',
        client: MockClient((req) async {
          calls.add('${req.method} ${req.url.path}');
          return _json({
            'id': 'p1', 'title': 'T', 'createdAt': '2030-01-01T10:00:00Z', 'description': 'D',
            'visibility': 'Text', 'pictures': [], 'videos': [], 'managerUsers': [], 'status': 'Draft',
          });
        }),
      );
      final service = PostService(api);
      await service.approve('p1');
      await service.reject('p1');
      expect(calls, ['POST /api/Posts/p1/approve', 'POST /api/Posts/p1/reject']);
    });

    test('adres odtwarzania zawiera token tylko po zalogowaniu', () {
      final api = ApiClient(baseUrl: 'http://test');
      expect(api.playbackUrl('k.mp4'), 'http://test/api/Media/k.mp4');
      api.setToken('a b');
      expect(api.playbackUrl('k.mp4'), 'http://test/api/Media/k.mp4?access_token=a+b');
    });
  });

  group('AppMessenger', () {
    test('identyczny komunikat nie powtarza się na liście', () {
      final m = AppMessenger();
      m.show('Post edytuje Jan');
      m.show('Post edytuje Jan');
      m.show('Post edytuje Jan');
      expect(m.messages, ['Post edytuje Jan']);
      m.dispose();
    });

    test('różne komunikaty tworzą stos (najnowszy na końcu) ograniczony do maxVisible', () {
      final m = AppMessenger(maxVisible: 2);
      m.show('a');
      m.show('b');
      m.show('c');
      expect(m.messages, ['b', 'c']);
      m.dismiss('b');
      expect(m.messages, ['c']);
      m.dispose();
    });

    test('komunikat znika po czasie życia', () async {
      final m = AppMessenger(lifetime: const Duration(milliseconds: 20));
      m.show('x');
      expect(m.messages, ['x']);
      await Future<void>.delayed(const Duration(milliseconds: 60));
      expect(m.messages, isEmpty);
      m.dispose();
    });
  });
}

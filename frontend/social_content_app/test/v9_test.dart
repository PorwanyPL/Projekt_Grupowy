import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:provider/provider.dart';
import 'package:social_content_app/models/app_notification.dart';
import 'package:social_content_app/models/chat.dart';
import 'package:social_content_app/models/post.dart';
import 'package:social_content_app/providers/home_navigation.dart';
import 'package:social_content_app/providers/post_provider.dart';
import 'package:social_content_app/providers/social_account_provider.dart';
import 'package:social_content_app/services/api_client.dart';
import 'package:social_content_app/services/chat_service.dart';
import 'package:social_content_app/services/media_service.dart';
import 'package:social_content_app/services/post_service.dart';
import 'package:social_content_app/services/social_account_service.dart';
import 'package:social_content_app/widgets/app_messenger.dart';
import 'package:social_content_app/widgets/media_player.dart';
import 'package:social_content_app/widgets/post_preview_dialog.dart';

Map<String, dynamic> _post({String status = 'AwaitingApproval', bool canApprove = true, String? comment}) => {
      'id': 'g1', 'title': 'Post do akceptacji', 'createdAt': '2030-01-01T10:00:00Z', 'description': 'Treść',
      'visibility': 'Text', 'pictures': [], 'videos': [], 'managerUsers': [],
      'status': status, 'canApprove': canApprove, 'rejectionComment': comment,
    };

void main() {
  group('Powiadomienia z celem', () {
    test('fromJson czyta typ i identyfikator celu; brak celu = nieklikalne', () {
      final chat = AppNotification.fromJson({
        'id': 'n1', 'message': 'Nowa wiadomość', 'createdAt': '2030-01-01T10:00:00Z', 'isRead': false,
        'targetType': 'Chat', 'targetId': 'c1',
      });
      expect(chat.targetType, NotificationTarget.chat);
      expect(chat.hasTarget, isTrue);

      final post = AppNotification.fromJson({
        'id': 'n2', 'message': 'Post', 'createdAt': '2030-01-01T10:00:00Z', 'isRead': true,
        'postId': 'g1', 'targetType': 'Post', 'targetId': 'g1',
      });
      expect(post.targetType, NotificationTarget.post);
      expect(post.targetId, 'g1');

      final none = AppNotification.fromJson({'id': 'n3', 'message': 'x', 'createdAt': '2030-01-01T10:00:00Z', 'isRead': false});
      expect(none.targetType, NotificationTarget.none);
      expect(none.hasTarget, isFalse);
    });

    test('HomeNavigation przekazuje prośbę o otwarcie rozmowy dokładnie raz', () {
      final nav = HomeNavigation();
      var calls = 0;
      nav.addListener(() => calls++);
      nav.openChat('c1');
      expect(calls, 1);
      expect(nav.takePendingConversation(), 'c1');
      expect(nav.takePendingConversation(), isNull);
    });
  });

  group('Wyszukiwanie rozmówcy', () {
    Future<Uri> capture(String? search, ContactSearchMode mode) async {
      late Uri seen;
      final client = MockClient((req) async {
        seen = req.url;
        return http.Response(
            jsonEncode([
              {'userId': 'u1', 'role': 'ClientUser', 'name': 'Karol Klient', 'description': 'Klient', 'email': 'k@x.pl'}
            ]),
            200,
            headers: {'content-type': 'application/json'});
      });
      final list = await ChatService(ApiClient(client: client, baseUrl: 'http://x')).contacts(search: search, mode: mode);
      expect(list.single.email, 'k@x.pl');
      return seen;
    }

    test('fraza i tryb idą do serwera jako search i mode', () async {
      final url = await capture('  kow ', ContactSearchMode.contains);
      expect(url.path, '/api/Chat/contacts');
      expect(url.queryParameters, {'search': 'kow', 'mode': 'Contains'});
      expect((await capture('a@b', ContactSearchMode.email)).queryParameters['mode'], 'Email');
      expect((await capture('ow', ContactSearchMode.contains)).queryParameters['mode'], 'Contains');
    });

    test('pusta fraza nie dodaje parametrów', () async {
      expect((await capture('   ', ContactSearchMode.email)).queryParameters, isEmpty);
      expect((await capture(null, ContactSearchMode.contains)).queryParameters, isEmpty);
    });
  });

  group('Odrzucenie posta z uwagami', () {
    test('Post.fromJson czyta rejectionComment', () {
      expect(Post.fromJson(_post(status: 'Rejected', comment: 'Zmień zdjęcie')).rejectionComment, 'Zmień zdjęcie');
      expect(Post.fromJson(_post()).rejectionComment, isNull);
    });

    test('PostService.reject wysyła uwagi w body, a bez uwag - bez body', () async {
      final bodies = <String>[];
      final client = MockClient((req) async {
        bodies.add(req.body);
        return http.Response(jsonEncode(_post(status: 'Rejected', canApprove: false)), 200,
            headers: {'content-type': 'application/json'});
      });
      final service = PostService(ApiClient(client: client, baseUrl: 'http://x'));
      await service.reject('g1', comment: '  Popraw tytuł  ');
      await service.reject('g1');
      expect(jsonDecode(bodies[0]), {'comment': 'Popraw tytuł'});
      expect(bodies[1], isEmpty);
    });

    testWidgets('klient odrzuca post w oknie z uwagami: komentarz trafia do API', (tester) async {
      final requests = <http.Request>[];
      final client = MockClient((req) async {
        requests.add(req);
        return http.Response(jsonEncode(_post(status: 'Rejected', canApprove: false, comment: 'Popraw tytuł')), 200,
            headers: {'content-type': 'application/json'});
      });
      final api = ApiClient(client: client, baseUrl: 'http://x');
      await tester.pumpWidget(MultiProvider(
        providers: [
          ChangeNotifierProvider(create: (_) => PostProvider(PostService(api))),
          ChangeNotifierProvider(create: (_) => SocialAccountProvider(SocialAccountService(api))),
          ChangeNotifierProvider(create: (_) => AppMessenger()),
          Provider(create: (_) => MediaService(api)),
        ],
        child: MaterialApp(
          home: Builder(
            builder: (context) => Scaffold(
              body: TextButton(
                onPressed: () => showDialog<void>(
                    context: context, builder: (_) => PostPreviewDialog(post: Post.fromJson(_post()))),
                child: const Text('otwórz'),
              ),
            ),
          ),
        ),
      ));

      await tester.tap(find.text('otwórz'));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('awaiting-info')), findsOneWidget);

      await tester.tap(find.byKey(const Key('reject-post')));
      await tester.pumpAndSettle();
      await tester.enterText(find.byKey(const Key('reject-comment')), 'Popraw tytuł');
      await tester.tap(find.byKey(const Key('reject-confirm')));
      await tester.pumpAndSettle();

      final reject = requests.singleWhere((r) => r.url.path == '/api/Posts/g1/reject');
      expect(jsonDecode(reject.body), {'comment': 'Popraw tytuł'});
    });

    testWidgets('anulowanie okna uwag nie odrzuca posta', (tester) async {
      final requests = <http.Request>[];
      final client = MockClient((req) async {
        requests.add(req);
        return http.Response('{}', 200);
      });
      final api = ApiClient(client: client, baseUrl: 'http://x');
      await tester.pumpWidget(MultiProvider(
        providers: [
          ChangeNotifierProvider(create: (_) => PostProvider(PostService(api))),
          ChangeNotifierProvider(create: (_) => SocialAccountProvider(SocialAccountService(api))),
          ChangeNotifierProvider(create: (_) => AppMessenger()),
          Provider(create: (_) => MediaService(api)),
        ],
        child: MaterialApp(home: Scaffold(body: PostPreviewDialog(post: Post.fromJson(_post())))),
      ));

      await tester.tap(find.byKey(const Key('reject-post')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Anuluj'));
      await tester.pumpAndSettle();
      expect(requests, isEmpty);
    });
  });

  group('Odtwarzacz', () {
    test('describeMediaError nadal tłumaczy 12029 i błędy HTTP', () {
      expect(describeMediaError('WinHttpSendRequest failed12029'), contains('12029'));
    });
  });
}

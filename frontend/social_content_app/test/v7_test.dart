import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:social_content_app/models/enums.dart';
import 'package:social_content_app/models/post.dart';
import 'package:social_content_app/models/post_filter.dart';
import 'package:social_content_app/screens/calendar_screen.dart';
import 'package:social_content_app/services/api_client.dart';
import 'package:social_content_app/services/post_service.dart';
import 'package:social_content_app/widgets/post_filter_dialog.dart';

Map<String, dynamic> _post({String status = 'Scheduled'}) => {
      'id': 'g1', 'title': 'Weekend w kawiarni', 'createdAt': '2030-01-01T10:00:00Z', 'description': 'D',
      'visibility': 'Text', 'pictures': [], 'videos': [],
      'managerUsers': [
        {'id': 'm1', 'firstName': 'Anna', 'lastName': 'Nowak', 'age': 30, 'description': 'd'}
      ],
      'status': status, 'scheduledAt': '2030-01-05T12:00:00Z', 'publishedAt': '2030-01-06T08:00:00Z',
      'targetAccountId': 'a1', 'tags': ['weekend', 'kawa'], 'counterpartName': 'Kawiarnia',
      'targets': [
        {'postId': 'p1', 'accountId': 'a1', 'accountName': 'FB', 'platform': 'Facebook', 'status': 'Scheduled'},
        {'postId': 'p2', 'accountId': 'a2', 'accountName': 'IG', 'platform': 'Instagram', 'status': 'Scheduled'},
        {'postId': 'p3', 'accountId': 'a3', 'accountName': 'IG2', 'platform': 'Instagram', 'status': 'Scheduled'},
      ],
    };

void main() {
  group('PostFilter', () {
    test('pusty filtr nie dodaje parametrów, a liczba aktywnych kategorii się zgadza', () {
      expect(PostFilter.empty.isEmpty, isTrue);
      expect(PostFilter.empty.toQuery(), isEmpty);
      final f = PostFilter(
        statuses: {PostStatus.draft, PostStatus.awaitingApproval},
        platforms: {SocialPlatform.tikTok},
        tags: {'kawa'},
        from: DateTime(2030, 1, 1),
      );
      expect(f.activeCount, 4);
      final q = f.toQuery();
      expect(q['status'], ['Draft', 'AwaitingApproval']);
      expect(q['platform'], ['TikTok']);
      expect(q['tag'], ['kawa']);
      expect(q.containsKey('to'), isFalse);
    });

    test('GET /api/Posts wysyła listy jako powtórzone klucze', () async {
      late Uri url;
      final api = ApiClient(
        baseUrl: 'http://test',
        client: MockClient((req) async {
          url = req.url;
          return http.Response(jsonEncode([_post()]), 200,
              headers: {'content-type': 'application/json; charset=utf-8'});
        }),
      );
      final posts = await PostService(api).getAll(PostFilter(
        statuses: {PostStatus.draft, PostStatus.scheduled},
        accountIds: {'a1'},
        managerId: 'm1',
      ));
      expect(url.path, '/api/Posts');
      expect(url.queryParametersAll['status'], ['Draft', 'Scheduled']);
      expect(url.queryParametersAll['accountIds'], ['a1']);
      expect(url.queryParameters['managerId'], 'm1');
      expect(posts.single.targets.length, 3);
    });
  });

  group('Post v7', () {
    test('grupa wariantów: platformy bez powtórzeń, konta, tagi, klient', () {
      final p = Post.fromJson(_post());
      expect(p.platforms, [SocialPlatform.facebook, SocialPlatform.instagram]);
      expect(p.accountIds, ['a1', 'a2', 'a3']);
      expect(p.tags, ['weekend', 'kawa']);
      expect(p.counterpartName, 'Kawiarnia');
    });

    test('dzień w kalendarzu: termin dla zaplanowanych, publikacja dla opublikowanych', () {
      expect(Post.fromJson(_post()).calendarDate, DateTime.parse('2030-01-05T12:00:00Z').toLocal());
      expect(Post.fromJson(_post(status: 'Published')).calendarDate,
          DateTime.parse('2030-01-06T08:00:00Z').toLocal());
    });

    test('starsza odpowiedź bez targets korzysta z targetAccountId', () {
      final json = _post()..remove('targets')..remove('tags');
      final p = Post.fromJson(json);
      expect(p.platforms, isEmpty);
      expect(p.accountIds, ['a1']);
      expect(p.tags, isEmpty);
    });
  });

  group('widgety', () {
    testWidgets('kafelek kalendarza: tytuł, klient, inicjały managera i ikony platform', (tester) async {
      var tapped = false;
      await tester.pumpWidget(MaterialApp(
        home: Scaffold(
          body: SizedBox(
            width: 220,
            height: 54,
            child: CalendarPostTile(post: Post.fromJson(_post()), onTap: () => tapped = true),
          ),
        ),
      ));
      expect(find.text('Weekend w kawiarni'), findsOneWidget);
      expect(find.text('Kawiarnia'), findsOneWidget);
      expect(find.text('AN'), findsOneWidget);
      expect(find.byKey(const Key('platform-facebook')), findsOneWidget);
      expect(find.byKey(const Key('platform-instagram')), findsOneWidget); // dwa konta IG = jedna ikona
      await tester.tap(find.byKey(const Key('tile-g1')));
      expect(tapped, isTrue);
    });

    testWidgets('okno filtrów zwraca wybrane statusy i platformy, a Resetuj je czyści', (tester) async {
      PostFilter? result;
      await tester.pumpWidget(MaterialApp(
        home: Builder(
          builder: (context) => TextButton(
            onPressed: () async =>
                result = await showPostFilterDialog(context, initial: PostFilter.empty, showDates: true),
            child: const Text('otwórz'),
          ),
        ),
      ));
      await tester.tap(find.text('otwórz'));
      await tester.pumpAndSettle();

      for (final key in const ['filter-status-draft', 'filter-status-failed', 'filter-platform-instagram']) {
        await tester.ensureVisible(find.byKey(Key(key)));
        await tester.tap(find.byKey(Key(key)));
        await tester.pump();
      }
      await tester.tap(find.byKey(const Key('filter-save')));
      await tester.pumpAndSettle();
      expect(result!.statuses, {PostStatus.draft, PostStatus.failed});
      expect(result!.platforms, {SocialPlatform.instagram});

      await tester.tap(find.text('otwórz'));
      await tester.pumpAndSettle();
      await tester.ensureVisible(find.byKey(const Key('filter-status-draft')));
      await tester.tap(find.byKey(const Key('filter-status-draft')));
      await tester.pump();
      await tester.tap(find.byKey(const Key('filter-reset')));
      await tester.pump();
      await tester.tap(find.byKey(const Key('filter-save')));
      await tester.pumpAndSettle();
      expect(result!.isEmpty, isTrue);
    });
  });
}

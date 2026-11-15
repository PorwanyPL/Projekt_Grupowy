import 'dart:async';
import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:social_content_app/models/app_notification.dart';
import 'package:social_content_app/models/auth.dart';
import 'package:social_content_app/models/chat.dart';
import 'package:social_content_app/providers/chat_provider.dart';
import 'package:social_content_app/providers/notification_provider.dart';
import 'package:social_content_app/services/api_client.dart';
import 'package:social_content_app/services/chat_realtime.dart';
import 'package:social_content_app/services/chat_service.dart';
import 'package:social_content_app/services/notification_service.dart';
import 'package:social_content_app/widgets/media_player.dart';

class FakeRealtime implements ChatRealtime {
  final _messages = StreamController<ChatMessage>.broadcast();
  final _notifications = StreamController<AppNotification>.broadcast();
  bool connected = false;
  int connectCalls = 0;

  @override
  Stream<ChatMessage> get messages => _messages.stream;
  @override
  Stream<AppNotification> get notifications => _notifications.stream;
  @override
  bool get isConnected => connected;
  @override
  Future<void> connect() async {
    connectCalls++;
    connected = true;
  }

  @override
  Future<void> disconnect() async => connected = false;

  void push(ChatMessage m) => _messages.add(m);
  void pushNotification(AppNotification n) => _notifications.add(n);
}

Map<String, dynamic> _msgJson(String id, {String sender = 'm1', String role = 'ManagerUser', String text = 'Cześć'}) => {
      'id': id, 'conversationId': 'c1', 'senderId': sender, 'senderRole': role,
      'senderName': 'Anna Nowak', 'text': text, 'sentAt': '2030-01-01T10:00:00',
    };

Map<String, dynamic> _convJson({int unread = 0}) => {
      'id': 'c1', 'kind': 'Direct', 'title': 'Anna Nowak',
      'participants': [
        {'userId': 'm1', 'role': 'ManagerUser', 'name': 'Anna Nowak'},
        {'userId': 'k1', 'role': 'ClientUser', 'name': 'Karol'},
      ],
      'lastMessage': null, 'unreadCount': unread,
    };

ChatProvider _provider(FakeRealtime rt, List<String> calls) {
  final client = MockClient((req) async {
    calls.add('${req.method} ${req.url.path}');
    if (req.url.path == '/api/Chat/conversations') {
      return http.Response(jsonEncode([_convJson()]), 200, headers: {'content-type': 'application/json'});
    }
    if (req.url.path.endsWith('/messages') && req.method == 'GET') {
      return http.Response(jsonEncode([_msgJson('x1')]), 200, headers: {'content-type': 'application/json'});
    }
    if (req.url.path.endsWith('/messages') && req.method == 'POST') {
      return http.Response(jsonEncode(_msgJson('x2', sender: 'k1', role: 'ClientUser', text: 'Hej')), 200,
          headers: {'content-type': 'application/json'});
    }
    return http.Response('', 204);
  });
  return ChatProvider(ChatService(ApiClient(client: client)), rt, healthCheckInterval: const Duration(hours: 1));
}

void main() {
  test('ChatMessage.fromJson traktuje czas bez strefy jako UTC', () {
    final m = ChatMessage.fromJson(_msgJson('a'));
    expect(m.sentAt.toUtc().hour, 10);
    expect(m.senderRole, UserRole.managerUser);
    expect(m.isMine('m1', UserRole.managerUser), isTrue);
    expect(m.isMine('m1', UserRole.clientUser), isFalse);
  });

  test('start ładuje rozmowy i łączy z hubem; wiadomość na żywo zwiększa licznik nieprzeczytanych', () async {
    final rt = FakeRealtime();
    final p = _provider(rt, []);
    final toasts = <String>[];
    p.onIncoming = toasts.add;
    await p.start('k1', UserRole.clientUser);

    expect(rt.connected, isTrue);
    expect(p.conversations, hasLength(1));
    rt.push(ChatMessage.fromJson(_msgJson('n1', text: 'Nowa')));
    await Future<void>.delayed(Duration.zero);

    expect(p.totalUnread, 1);
    expect(p.messagesOf('c1').single.text, 'Nowa');
    expect(toasts.single, contains('Nowa'));
    await p.stop();
  });

  test('otwarta rozmowa nie zwiększa licznika i jest oznaczana jako przeczytana; duplikat po Id jest pomijany', () async {
    final rt = FakeRealtime();
    final calls = <String>[];
    final p = _provider(rt, calls);
    await p.start('k1', UserRole.clientUser);
    await p.openConversation('c1');

    rt.push(ChatMessage.fromJson(_msgJson('n2')));
    rt.push(ChatMessage.fromJson(_msgJson('n2')));
    await Future<void>.delayed(Duration.zero);

    expect(p.totalUnread, 0);
    expect(p.messagesOf('c1').where((m) => m.id == 'n2'), hasLength(1));
    expect(calls, contains('POST /api/Chat/conversations/c1/read'));
    await p.stop();
  });

  test('wysłana wiadomość nie jest liczona jako nieprzeczytana, a echo z huba nie dubluje jej', () async {
    final rt = FakeRealtime();
    final p = _provider(rt, []);
    await p.start('k1', UserRole.clientUser);
    await p.send('c1', 'Hej');
    rt.push(ChatMessage.fromJson(_msgJson('x2', sender: 'k1', role: 'ClientUser', text: 'Hej')));
    await Future<void>.delayed(Duration.zero);

    expect(p.messagesOf('c1').where((m) => m.id == 'x2'), hasLength(1));
    expect(p.totalUnread, 0);
    await p.stop();
  });

  test('powiadomienie z huba trafia do callbacku, a NotificationProvider.push nie dubluje', () async {
    final rt = FakeRealtime();
    final p = _provider(rt, []);
    AppNotification? got;
    p.onNotification = (n) => got = n;
    await p.start('k1', UserRole.clientUser);
    final n = AppNotification.fromJson({'id': 'n1', 'message': 'Post opublikowany', 'createdAt': '2030-01-01T10:00:00Z', 'isRead': false});
    rt.pushNotification(n);
    await Future<void>.delayed(Duration.zero);
    expect(got?.id, 'n1');

    final np = NotificationProvider(NotificationService(ApiClient(client: MockClient((_) async => http.Response('[]', 200)))));
    np.push(n);
    np.push(n);
    expect(np.items, hasLength(1));
    expect(np.unread, 1);
    await p.stop();
  });

  test('describeMediaError tłumaczy błąd 12029 na czytelny komunikat', () {
    expect(describeMediaError('WinHttpSendRequest failed12029'), contains('12029'));
    expect(describeMediaError(TimeoutException('x')), contains('na czas'));
    expect(describeMediaError('cokolwiek'), 'Nie można odtworzyć pliku.');
  });
}

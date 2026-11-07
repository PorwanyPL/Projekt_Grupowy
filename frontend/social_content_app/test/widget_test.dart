import 'package:flutter_test/flutter_test.dart';
import 'package:social_content_app/models/enums.dart';
import 'package:social_content_app/models/post.dart';
import 'package:social_content_app/utils/validators.dart';

void main() {
  test('walidacja imienia', () {
    expect(FormValidators.name('Anna', 'Imię'), isNull);
    expect(FormValidators.name("O'Neil-Żak", 'Imię'), isNull);
    expect(FormValidators.name('', 'Imię'), isNotNull);
    expect(FormValidators.name('A1', 'Imię'), isNotNull);
    expect(FormValidators.name('-Anna', 'Imię'), isNotNull);
  });

  test('walidacja wieku', () {
    expect(FormValidators.age('17'), isNotNull);
    expect(FormValidators.age('18'), isNull);
    expect(FormValidators.age('101'), isNotNull);
    expect(FormValidators.age('abc'), isNotNull);
  });

  test('PostInput serializuje się zgodnie z PostWriteDto (klucze plików, publikacja, UTC)', () {
    final when = DateTime(2030, 5, 1, 12, 30);
    final input = PostInput(
      title: 'T',
      description: 'D',
      type: PostType.photo,
      pictures: const [
        MediaInput(storageKey: 'abc.png', fileName: 'a.png', format: MediaFormat.png, sizeKb: 10),
      ],
      videos: const [],
      targetAccountId: 'acc-1',
      scheduledAt: when,
    );
    final json = input.toJson();
    expect(json['visibility'], 'Photo');
    expect((json['pictures'] as List).first, {'storageKey': 'abc.png', 'fileName': 'a.png'});
    expect(json.containsKey('managerUserIds'), isFalse); // współautorów ustawia serwer
    expect(json['publishNow'], false);
    expect(json['scheduledAt'], when.toUtc().toIso8601String());
    expect(json['targetAccountId'], 'acc-1');

    const now = PostInput(
      title: 'T', description: 'D', type: PostType.text, pictures: [], videos: [],
      targetAccountId: 'acc-1', publishNow: true);
    expect(now.toJson()['publishNow'], true);
    expect(now.toJson().containsKey('scheduledAt'), isFalse);
  });

  test('Post.fromJson czyta status, uprawnienia, współautorów i blokadę', () {
    final post = Post.fromJson({
      'id': 'p1', 'title': 'T', 'createdAt': '2030-01-01T10:00:00Z', 'description': 'D',
      'visibility': 'Text', 'pictures': [], 'videos': [],
      'managerUsers': [
        {'id': 'm1', 'firstName': 'A', 'lastName': 'B', 'age': 30, 'description': 'd'}
      ],
      'status': 'Failed', 'errorMessage': 'Token wygasł.', 'targetAccountId': 'acc-1',
      'canEdit': true, 'canDelete': false,
      'lock': {'holderName': 'Jan Kowalski', 'expiresAt': '2030-01-01T10:02:00Z', 'heldByMe': false},
    });
    expect(post.status, PostStatus.failed);
    expect(post.errorMessage, 'Token wygasł.');
    expect(post.canEdit, isTrue);
    expect(post.canDelete, isFalse);
    expect(post.managers.single.fullName, 'A B');
    expect(post.lockedByOther, isTrue);
    expect(post.lock!.holderName, 'Jan Kowalski');
  });

  test('wideo bez znanej długości i zdjęcia mają storageKey', () {
    final v = VideoItem.fromJson({
      'id': 'v', 'fileName': 'f.mp4', 'format': 'Mp4', 'sizeKb': 5, 'length': null,
      'storageKey': 'k.mp4'});
    expect(v.length, isNull);
    expect(v.storageKey, 'k.mp4');
  });
}

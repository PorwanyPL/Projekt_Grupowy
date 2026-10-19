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

  test('PostInput serializuje się zgodnie z PostWriteDto', () {
    const input = PostInput(
      title: 'T',
      description: 'D',
      type: PostType.photo,
      pictures: [
        MediaInput(fileName: 'a.png', format: MediaFormat.png, amount: 10),
      ],
      videos: [],
      managerIds: ['id-1'],
    );
    final json = input.toJson();
    expect(json['visibility'], 'Photo');
    expect((json['pictures'] as List).first['format'], 'Png');
    expect((json['pictures'] as List).first['sizeKb'], 10);
    expect(json['managerUserIds'], ['id-1']);
  });
}

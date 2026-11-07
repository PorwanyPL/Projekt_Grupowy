import 'dart:typed_data';

import '../models/post.dart';
import 'api_client.dart';

/// Przesyłanie plików mediów na serwer (walidacja typu i rozmiaru po stronie serwera).
class MediaService {
  MediaService(this._api);

  final ApiClient _api;

  Future<MediaInput> upload(Uint8List bytes, String fileName) async {
    final json = await _api.postFile('/api/Media', bytes, fileName);
    return MediaInput.fromUploadJson(json as Map<String, dynamic>);
  }

  String urlFor(String storageKey) => _api.mediaUrl(storageKey);

  Map<String, String> get headers => _api.authHeaders;
}

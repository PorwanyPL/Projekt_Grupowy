import 'package:flutter/foundation.dart';

/// Adres backendu.
///
/// Nadpisz przy uruchomieniu, np. dla telefonu w tej samej sieci Wi-Fi:
///   flutter run --dart-define=API_BASE_URL=http://192.168.1.20:5080
class ApiConfig {
  static const String _override = String.fromEnvironment('API_BASE_URL');

  static String get baseUrl {
    if (_override.isNotEmpty) return _override;
    // Emulator Androida widzi komputer hosta pod 10.0.2.2.
    if (!kIsWeb && defaultTargetPlatform == TargetPlatform.android) {
      return 'http://10.0.2.2:5080';
    }
    // 127.0.0.1 zamiast localhost: Windows rozwiązuje localhost najpierw na IPv6 (::1), a natywne odtwarzacze
    // (WinHTTP w video_player_win) potrafią wtedy zgłosić błąd 12029 (nie można się połączyć).
    return 'http://127.0.0.1:5080';
  }

  /// Czy adres jest bezpieczny: HTTPS albo HTTP wyłącznie do komputera/emulatora deweloperskiego
  /// (127.0.0.1, localhost, 10.0.2.2), gdzie ruch nie opuszcza maszyny.
  static bool isSecureOrLoopback(String url) {
    final uri = Uri.tryParse(url);
    if (uri == null) return false;
    if (uri.scheme == 'https') return true;
    return uri.scheme == 'http' && const {'127.0.0.1', 'localhost', '::1', '10.0.2.2'}.contains(uri.host);
  }

  /// W wersji release odmawia pracy z adresem HTTP spoza pętli zwrotnej: hasła, kody i tokeny nie mogą iść jawnym tekstem.
  /// W debugowaniu (np. telefon w sieci Wi-Fi) dozwolone, ale z ostrzeżeniem w konsoli.
  static void assertSecure(String url) {
    if (isSecureOrLoopback(url)) return;
    if (kReleaseMode) {
      throw StateError('Adres API ($url) nie używa HTTPS. Zbuduj aplikację z --dart-define=API_BASE_URL=https://...');
    }
    debugPrint('UWAGA: adres API ($url) nie używa HTTPS - ruch (hasła, tokeny) idzie jawnym tekstem.');
  }

  static const Duration timeout = Duration(seconds: 15);
}

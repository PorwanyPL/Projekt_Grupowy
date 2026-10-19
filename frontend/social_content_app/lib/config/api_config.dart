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
    return 'http://localhost:5080';
  }

  static const Duration timeout = Duration(seconds: 15);
}

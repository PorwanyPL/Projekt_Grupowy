import 'dart:async';
import 'dart:io';

import 'package:flutter/foundation.dart' show kIsWeb;
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:provider/provider.dart';
import 'package:video_player/video_player.dart';

import '../models/enums.dart';
import 'app_messenger.dart';

/// Odtwarzacz wideo i audio (mp4, mp3, flac) dla podglądu posta: play/pauza, pasek postępu i przewijanie.
/// Plik jest strumieniowany z GET /api/Media (serwer obsługuje zakresy bajtów, więc przewijanie działa).
/// Na Windows odtwarzanie zapewnia pakiet video_player_win (rejestruje się sam po dodaniu do pubspec).
///
/// Inicjalizacja jest zabezpieczona przed "wiecznym spinnerem": ma limit czasu [initTimeout], po którym pokazuje błąd
/// z przyciskiem ponowienia; porzucony (zdjęty z drzewa lub podmieniony) kontroler jest zwalniany, a jego spóźniony wynik
/// ignorowany. Gdy podano [filePath] (plik świeżo wybrany z dysku), podgląd odtwarza go lokalnie - bez pobierania z serwera -
/// a dopiero przy błędzie próbuje adresu [url]. Przy [lazy] odtwarzacz startuje dopiero po kliknięciu (formularz posta:
/// natywny odtwarzacz nie jest tworzony przy każdym otwarciu formularza ani po każdej zmianie pola).
/// Czytelny opis błędu odtwarzacza. Natywny odtwarzacz Windows zgłasza np. "WinHttpSendRequest failed12029"
/// (ERROR_WINHTTP_CANNOT_CONNECT) - zwykle serwer nie słucha pod tym adresem/portem albo `localhost` wskazuje IPv6.
String describeMediaError(Object? error) {
  final text = '$error';
  if (text.contains('12029') || text.contains('WinHttp')) {
    return 'Nie można połączyć się z serwerem multimediów (błąd 12029). Sprawdź, czy backend działa '
        'i czy adres API w aplikacji (API_BASE_URL) zgadza się z adresem serwera.';
  }
  if (error is HttpException) return 'Serwer odrzucił pobranie pliku: ${error.message}';
  if (error is SocketException) return 'Nie można połączyć się z serwerem. Sprawdź, czy backend działa pod adresem API.';
  if (error is TimeoutException) return 'Odtwarzacz nie odpowiedział na czas. Spróbuj ponownie.';
  return 'Nie można odtworzyć pliku.';
}

class MediaPlayer extends StatefulWidget {
  const MediaPlayer({
    super.key,
    required this.url,
    required this.format,
    this.headers = const {},
    this.title,
    this.filePath,
    this.lazy = false,
    this.initTimeout = const Duration(seconds: 15),
  });

  final String url;
  final MediaFormat format;
  final Map<String, String> headers;
  final String? title;

  /// Lokalna ścieżka pliku (null = tylko adres sieciowy).
  final String? filePath;
  final bool lazy;
  final Duration initTimeout;

  bool get isAudio => format == MediaFormat.mp3 || format == MediaFormat.flac;

  @override
  State<MediaPlayer> createState() => _MediaPlayerState();
}

class _MediaPlayerState extends State<MediaPlayer> {
  VideoPlayerController? _controller;
  Object? _error;
  bool _started = false;

  /// Numer próby inicjalizacji; wynik starszej próby jest odrzucany.
  int _generation = 0;

  @override
  void initState() {
    super.initState();
    if (!widget.lazy) _start();
  }

  @override
  void didUpdateWidget(MediaPlayer old) {
    super.didUpdateWidget(old);
    if (old.url != widget.url || old.filePath != widget.filePath) {
      _release();
      _error = null;
      _started = false;
      if (!widget.lazy) _start();
    }
  }

  void _release() {
    _generation++;
    final old = _controller;
    _controller = null;
    old?.dispose();
  }

  /// Na Windows natywny odtwarzacz (WinHTTP w video_player_win) bywa nie w stanie połączyć się z backendem
  /// (błąd 12029: proxy systemowe, VPN, zapora, IPv6), mimo że zwykłe żądania z aplikacji (dart:io) działają.
  /// Dlatego plik pobieramy własnym klientem HTTP do katalogu tymczasowego i odtwarzamy lokalnie - to omija WinHTTP.
  Future<File?> _downloadToTemp() async {
    if (kIsWeb || !Platform.isWindows) return null;
    final base = widget.url.split('?').first;
    final dot = base.lastIndexOf('.');
    final ext = dot > base.lastIndexOf('/') ? base.substring(dot) : '.bin';
    final file = File('${Directory.systemTemp.path}${Platform.pathSeparator}sc_media_${base.hashCode.toUnsigned(32)}$ext');
    if (file.existsSync() && file.lengthSync() > 0) return file;
    final response = await http
        .get(Uri.parse(widget.url), headers: widget.headers)
        .timeout(const Duration(seconds: 120));
    if (response.statusCode != 200 || response.bodyBytes.isEmpty) {
      throw HttpException('Serwer zwrócił ${response.statusCode}', uri: Uri.parse(base));
    }
    await file.writeAsBytes(response.bodyBytes, flush: true);
    return file;
  }

  VideoPlayerController _networkController() => VideoPlayerController.networkUrl(
        Uri.parse(widget.url),
        httpHeaders: widget.headers,
      );

  Future<void> _start() async {
    final generation = ++_generation;
    setState(() {
      _started = true;
      _error = null;
    });

    final path = widget.filePath;
    final candidates = <VideoPlayerController Function()>[
      if (path != null && path.isNotEmpty && File(path).existsSync()) () => VideoPlayerController.file(File(path)),
      _networkController,
    ];

    Object? lastError;
    if (candidates.length == 1) {
      // Brak lokalnego pliku: spróbuj pobrać plik własnym klientem HTTP (Windows), zanim sięgniemy po odtwarzacz sieciowy.
      try {
        final downloaded = await _downloadToTemp();
        if (!mounted || generation != _generation) return;
        if (downloaded != null) candidates.insert(0, () => VideoPlayerController.file(downloaded));
      } catch (e) {
        lastError = e;
        if (!mounted || generation != _generation) return;
      }
    }
    for (final create in candidates) {
      final controller = create();
      _controller = controller;
      try {
        await controller.initialize().timeout(
              widget.initTimeout,
              onTimeout: () => throw TimeoutException(
                  'Odtwarzacz nie odpowiedział w ciągu ${widget.initTimeout.inSeconds} s.'),
            );
        await controller.setLooping(false);
        if (!mounted || generation != _generation) {
          await controller.dispose();
          return;
        }
        setState(() {});
        return;
      } catch (e) {
        lastError = e;
        if (!mounted || generation != _generation) {
          await controller.dispose();
          return;
        }
        await controller.dispose();
        if (identical(_controller, controller)) _controller = null;
      }
    }
    if (mounted && generation == _generation) {
      setState(() => _error = lastError ?? 'Nieznany błąd');
      // Czytelny komunikat w stosie komunikatów (jeśli aplikacja go udostępnia); ekran nie jest blokowany.
      try {
        context.read<AppMessenger>().show(describeMediaError(lastError));
      } catch (_) {}
    }
  }

  @override
  void dispose() {
    _generation++;
    _controller?.dispose();
    super.dispose();
  }

  void _toggle() {
    final c = _controller;
    if (c == null || !c.value.isInitialized) return;
    c.value.isPlaying ? c.pause() : c.play();
  }

  @override
  Widget build(BuildContext context) {
    final c = _controller;
    if (_error != null) {
      return ColoredBox(
        color: Colors.black87,
        child: Center(
          child: Padding(
            padding: const EdgeInsets.all(12),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(
                  '${describeMediaError(_error)}${widget.title == null ? '' : ' (${widget.title})'}',
                  textAlign: TextAlign.center,
                  style: const TextStyle(color: Colors.white70),
                ),
                const SizedBox(height: 8),
                TextButton.icon(
                  key: const Key('media-retry'),
                  onPressed: _start,
                  icon: const Icon(Icons.refresh, color: Colors.white),
                  label: const Text('Spróbuj ponownie', style: TextStyle(color: Colors.white)),
                ),
              ],
            ),
          ),
        ),
      );
    }
    if (!_started) {
      return ColoredBox(
        color: Colors.black87,
        child: InkWell(
          key: const Key('media-load'),
          onTap: _start,
          child: Center(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Icon(widget.isAudio ? Icons.audiotrack : Icons.play_circle_outline,
                    color: Colors.white, size: 56),
                if (widget.title != null)
                  Padding(
                    padding: const EdgeInsets.all(8),
                    child: Text(widget.title!,
                        style: const TextStyle(color: Colors.white70), overflow: TextOverflow.ellipsis),
                  ),
              ],
            ),
          ),
        ),
      );
    }
    if (c == null || !c.value.isInitialized) {
      return const ColoredBox(
        color: Colors.black87,
        child: Center(child: CircularProgressIndicator()),
      );
    }

    return ColoredBox(
      color: Colors.black87,
      child: Stack(
        fit: StackFit.expand,
        children: [
          if (widget.isAudio)
            Center(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  const Icon(Icons.audiotrack, color: Colors.white, size: 48),
                  if (widget.title != null)
                    Padding(
                      padding: const EdgeInsets.all(8),
                      child: Text(widget.title!,
                          style: const TextStyle(color: Colors.white70),
                          overflow: TextOverflow.ellipsis),
                    ),
                ],
              ),
            )
          else
            Center(
              child: AspectRatio(aspectRatio: c.value.aspectRatio, child: VideoPlayer(c)),
            ),
          Positioned(
            left: 0,
            right: 0,
            bottom: 0,
            child: ValueListenableBuilder<VideoPlayerValue>(
              valueListenable: c,
              builder: (_, value, __) => Row(
                children: [
                  IconButton(
                    key: const Key('media-play-pause'),
                    tooltip: value.isPlaying ? 'Pauza' : 'Odtwórz',
                    color: Colors.white,
                    icon: Icon(value.isPlaying ? Icons.pause : Icons.play_arrow),
                    onPressed: _toggle,
                  ),
                  Expanded(
                    child: VideoProgressIndicator(
                      c,
                      allowScrubbing: true,
                      padding: const EdgeInsets.symmetric(vertical: 12),
                    ),
                  ),
                  const SizedBox(width: 8),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}

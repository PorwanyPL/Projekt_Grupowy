import 'package:flutter/material.dart';
import 'package:video_player/video_player.dart';

import '../models/enums.dart';

/// Odtwarzacz wideo i audio (mp4, mp3, flac) dla podglądu posta: play/pauza, pasek postępu i przewijanie.
/// Plik jest strumieniowany z GET /api/Media (serwer obsługuje zakresy bajtów, więc przewijanie działa).
/// Na Windows odtwarzanie zapewnia pakiet video_player_win (rejestruje się sam po dodaniu do pubspec).
class MediaPlayer extends StatefulWidget {
  const MediaPlayer({
    super.key,
    required this.url,
    required this.format,
    this.headers = const {},
    this.title,
  });

  final String url;
  final MediaFormat format;
  final Map<String, String> headers;
  final String? title;

  bool get isAudio => format == MediaFormat.mp3 || format == MediaFormat.flac;

  @override
  State<MediaPlayer> createState() => _MediaPlayerState();
}

class _MediaPlayerState extends State<MediaPlayer> {
  VideoPlayerController? _controller;
  Object? _error;

  @override
  void initState() {
    super.initState();
    _init();
  }

  @override
  void didUpdateWidget(MediaPlayer old) {
    super.didUpdateWidget(old);
    if (old.url != widget.url) {
      _controller?.dispose();
      _controller = null;
      _error = null;
      _init();
    }
  }

  Future<void> _init() async {
    final controller = VideoPlayerController.networkUrl(
      Uri.parse(widget.url),
      httpHeaders: widget.headers,
    );
    _controller = controller;
    try {
      await controller.initialize();
      await controller.setLooping(false);
      if (mounted) setState(() {});
    } catch (e) {
      if (mounted) setState(() => _error = e);
    }
  }

  @override
  void dispose() {
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
            child: Text(
              'Nie można odtworzyć pliku${widget.title == null ? '' : ' ${widget.title}'}.',
              textAlign: TextAlign.center,
              style: const TextStyle(color: Colors.white70),
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

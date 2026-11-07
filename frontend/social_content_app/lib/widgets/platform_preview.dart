import 'package:flutter/material.dart';

import '../models/enums.dart';
import '../models/post.dart';

/// Podgląd posta w stylu platformy konta (Facebook, Instagram, TikTok) - aktualizuje się na bieżąco
/// podczas wpisywania. Zdjęcia pochodzą z serwera (już przesłane), więc wymagają nagłówków autoryzacji.
class PlatformPreview extends StatelessWidget {
  const PlatformPreview({
    super.key,
    required this.platform,
    required this.accountName,
    required this.title,
    required this.description,
    required this.pictures,
    required this.videos,
    required this.imageUrl,
    required this.imageHeaders,
  });

  final SocialPlatform platform;
  final String accountName;
  final String title;
  final String description;
  final List<MediaInput> pictures;
  final List<MediaInput> videos;
  final String Function(String storageKey) imageUrl;
  final Map<String, String> imageHeaders;

  String get _text {
    final t = title.trim();
    final d = description.trim();
    if (t.isEmpty && d.isEmpty) return 'Tu pojawi się treść posta.';
    return [if (t.isNotEmpty) t, if (d.isNotEmpty) d].join('\n');
  }

  Widget _image(MediaInput p, {BoxFit fit = BoxFit.cover}) => Image.network(
        imageUrl(p.storageKey),
        headers: imageHeaders,
        fit: fit,
        errorBuilder: (_, __, ___) => const ColoredBox(
          color: Colors.black12,
          child: Center(child: Icon(Icons.broken_image_outlined)),
        ),
      );

  Widget _videoPlaceholder(MediaInput v) => ColoredBox(
        color: Colors.black87,
        child: Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.play_circle_outline, color: Colors.white, size: 56),
              const SizedBox(height: 4),
              Text(v.fileName,
                  style: const TextStyle(color: Colors.white70),
                  overflow: TextOverflow.ellipsis),
            ],
          ),
        ),
      );

  /// Pierwsze medium: zdjęcie albo wideo (null, gdy post tekstowy).
  Widget? _media({BoxFit fit = BoxFit.cover}) {
    if (pictures.isNotEmpty) return _image(pictures.first, fit: fit);
    if (videos.isNotEmpty) return _videoPlaceholder(videos.first);
    return null;
  }

  @override
  Widget build(BuildContext context) {
    switch (platform) {
      case SocialPlatform.facebook:
        return _facebook(context);
      case SocialPlatform.instagram:
        return _instagram(context);
      case SocialPlatform.tikTok:
        return _tikTok(context);
    }
  }

  Widget _facebook(BuildContext context) {
    final media = _media();
    return Card(
      key: const Key('preview-facebook'),
      clipBehavior: Clip.antiAlias,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          ListTile(
            leading: const CircleAvatar(
                backgroundColor: Color(0xFF1877F2),
                child: Icon(Icons.facebook, color: Colors.white)),
            title: Text(accountName, style: const TextStyle(fontWeight: FontWeight.w600)),
            subtitle: const Text('Teraz · 🌐'),
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
            child: Text(_text),
          ),
          if (media != null) AspectRatio(aspectRatio: 16 / 9, child: media),
          const Divider(height: 1),
          const Padding(
            padding: EdgeInsets.symmetric(vertical: 8),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.spaceEvenly,
              children: [
                Text('👍 Lubię to'),
                Text('💬 Komentarz'),
                Text('↗ Udostępnij'),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _instagram(BuildContext context) {
    final media = _media();
    return Card(
      key: const Key('preview-instagram'),
      clipBehavior: Clip.antiAlias,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          ListTile(
            leading: const CircleAvatar(
                backgroundColor: Color(0xFFC13584),
                child: Icon(Icons.camera_alt_outlined, color: Colors.white)),
            title: Text(accountName, style: const TextStyle(fontWeight: FontWeight.w600)),
          ),
          AspectRatio(
            aspectRatio: 1,
            child: media ??
                const ColoredBox(
                  color: Colors.black12,
                  child: Center(child: Text('Instagram wymaga zdjęcia lub wideo')),
                ),
          ),
          const Padding(
            padding: EdgeInsets.fromLTRB(12, 8, 12, 0),
            child: Row(
              children: [
                Icon(Icons.favorite_border),
                SizedBox(width: 12),
                Icon(Icons.chat_bubble_outline),
                SizedBox(width: 12),
                Icon(Icons.send_outlined),
                Spacer(),
                Icon(Icons.bookmark_border),
              ],
            ),
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(12, 8, 12, 12),
            child: Text.rich(TextSpan(children: [
              TextSpan(text: '$accountName ', style: const TextStyle(fontWeight: FontWeight.w600)),
              TextSpan(text: _text),
            ])),
          ),
        ],
      ),
    );
  }

  Widget _tikTok(BuildContext context) {
    final media = _media();
    return Center(
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 260),
        child: AspectRatio(
          key: const Key('preview-tiktok'),
          aspectRatio: 9 / 16,
          child: ClipRRect(
            borderRadius: BorderRadius.circular(16),
            child: Stack(
              fit: StackFit.expand,
              children: [
                media ?? const ColoredBox(color: Colors.black),
                const DecoratedBox(
                  decoration: BoxDecoration(
                    gradient: LinearGradient(
                      begin: Alignment.topCenter,
                      end: Alignment.bottomCenter,
                      colors: [Colors.transparent, Colors.black87],
                    ),
                  ),
                ),
                const Positioned(
                  right: 10,
                  bottom: 80,
                  child: Column(
                    children: [
                      Icon(Icons.favorite, color: Colors.white),
                      SizedBox(height: 16),
                      Icon(Icons.chat_bubble, color: Colors.white),
                      SizedBox(height: 16),
                      Icon(Icons.reply, color: Colors.white),
                    ],
                  ),
                ),
                Positioned(
                  left: 12,
                  right: 48,
                  bottom: 12,
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text('@$accountName',
                          style: const TextStyle(
                              color: Colors.white, fontWeight: FontWeight.bold)),
                      const SizedBox(height: 4),
                      Text(_text,
                          maxLines: 4,
                          overflow: TextOverflow.ellipsis,
                          style: const TextStyle(color: Colors.white)),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

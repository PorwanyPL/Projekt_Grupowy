import 'package:flutter/material.dart';

import '../models/enums.dart';

IconData platformIcon(SocialPlatform p) => switch (p) {
      SocialPlatform.facebook => Icons.facebook,
      SocialPlatform.instagram => Icons.camera_alt_outlined,
      SocialPlatform.tikTok => Icons.music_note,
    };

Color platformColor(SocialPlatform p) => switch (p) {
      SocialPlatform.facebook => const Color(0xFF1877F2),
      SocialPlatform.instagram => const Color(0xFFC13584),
      SocialPlatform.tikTok => const Color(0xFF111111),
    };

/// Okrągła ikona platformy w jej kolorze.
class PlatformBadge extends StatelessWidget {
  const PlatformBadge({super.key, required this.platform, this.size = 20, this.dimmed = false});

  final SocialPlatform platform;
  final double size;
  final bool dimmed;

  @override
  Widget build(BuildContext context) {
    final color = dimmed ? Colors.grey : platformColor(platform);
    return Tooltip(
      message: platform.label,
      child: Container(
        key: Key('platform-${platform.name}'),
        width: size,
        height: size,
        decoration: BoxDecoration(color: color, shape: BoxShape.circle),
        child: Icon(platformIcon(platform), color: Colors.white, size: size * 0.64),
      ),
    );
  }
}

/// Rząd ikon platform posta (lista i kalendarz).
class PlatformBadges extends StatelessWidget {
  const PlatformBadges({super.key, required this.platforms, this.size = 18});

  final List<SocialPlatform> platforms;
  final double size;

  @override
  Widget build(BuildContext context) => Wrap(
        spacing: 3,
        runSpacing: 3,
        alignment: WrapAlignment.end,
        children: [for (final p in platforms) PlatformBadge(platform: p, size: size)],
      );
}

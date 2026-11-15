import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/enums.dart';
import '../models/post.dart';
import '../providers/post_provider.dart';
import '../providers/social_account_provider.dart';
import '../services/api_exception.dart';
import '../services/media_service.dart';
import '../utils/formatters.dart';
import 'app_messenger.dart';
import 'platform_preview.dart';

/// Podgląd posta tylko do odczytu: wygląd w stylu platformy konta z działającym odtwarzaczem wideo/audio,
/// status i metadane. Klient widzi tu przyciski zatwierdzenia/odrzucenia, gdy post czeka na jego decyzję.
class PostPreviewDialog extends StatefulWidget {
  const PostPreviewDialog({super.key, required this.post});

  final Post post;

  @override
  State<PostPreviewDialog> createState() => _PostPreviewDialogState();
}

class _PostPreviewDialogState extends State<PostPreviewDialog> {
  bool _busy = false;

  Future<void> _decide({required bool approve}) async {
    final posts = context.read<PostProvider>();
    final messenger = context.read<AppMessenger>();
    final nav = Navigator.of(context);
    setState(() => _busy = true);
    try {
      approve ? await posts.approve(widget.post.id) : await posts.reject(widget.post.id);
      messenger.show(approve ? 'Post został zatwierdzony.' : 'Post został odrzucony.');
      nav.pop();
    } on ApiException catch (e) {
      messenger.show(e.message);
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final post = widget.post;
    final media = context.read<MediaService>();
    final account = context
        .read<SocialAccountProvider>()
        .items
        .where((a) => a.id == post.targetAccountId)
        .firstOrNull;

    MediaInput toInput(String key, String name, MediaFormat f, int kb) =>
        MediaInput(storageKey: key, fileName: name, format: f, sizeKb: kb);

    return AlertDialog(
      title: Text(post.title),
      content: SizedBox(
        width: 460,
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              PlatformPreview(
                platform: account?.platform ?? SocialPlatform.facebook,
                accountName: account?.accountName ?? 'Konto',
                title: post.title,
                description: post.description,
                pictures: [
                  for (final p in post.pictures) toInput(p.storageKey, p.fileName, p.format, p.sizeKb)
                ],
                videos: [
                  for (final v in post.videos) toInput(v.storageKey, v.fileName, v.format, v.sizeKb)
                ],
                imageUrl: media.urlFor,
                imageHeaders: media.headers,
                videoUrl: media.playbackUrlFor,
                videoHeaders: media.headers,
              ),
              const SizedBox(height: 12),
              Text('Status: ${post.status.label}'),
              if (post.errorMessage != null) Text('Błąd: ${post.errorMessage}'),
              Text('Typ: ${post.visibility.label}'),
              Text('Utworzono: ${formatDateTime(post.createdAt)}'),
              if (post.scheduledAt != null) Text('Zaplanowano na: ${formatDateTime(post.scheduledAt!)}'),
              if (post.publishedAt != null) Text('Opublikowano: ${formatDateTime(post.publishedAt!)}'),
              if (post.managers.isNotEmpty)
                Text('Autorzy i współautorzy: ${post.managers.map((m) => m.fullName).join(', ')}'),
            ],
          ),
        ),
      ),
      actions: [
        if (post.canApprove) ...[
          OutlinedButton(
            key: const Key('reject-post'),
            onPressed: _busy ? null : () => _decide(approve: false),
            child: const Text('Odrzuć'),
          ),
          FilledButton(
            key: const Key('approve-post'),
            onPressed: _busy ? null : () => _decide(approve: true),
            child: const Text('Zatwierdź'),
          ),
        ],
        TextButton(
          onPressed: _busy ? null : () => Navigator.pop(context),
          child: const Text('Zamknij'),
        ),
      ],
    );
  }
}

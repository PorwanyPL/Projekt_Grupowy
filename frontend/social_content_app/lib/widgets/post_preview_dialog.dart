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

  /// Odrzucenie: okno z opcjonalnymi uwagami do poprawy (trafiają do autorów w powiadomieniu na żywo).
  /// Zwraca tekst uwag (może być pusty) albo null po anulowaniu.
  Future<String?> _askRejectionComment() =>
      showDialog<String>(context: context, builder: (_) => const _RejectCommentDialog());

  Future<void> _decide({required bool approve}) async {
    final posts = context.read<PostProvider>();
    final messenger = context.read<AppMessenger>();
    final nav = Navigator.of(context);
    String? comment;
    if (!approve) {
      comment = await _askRejectionComment();
      if (comment == null || !mounted) return; // anulowano
    }
    setState(() => _busy = true);
    try {
      approve ? await posts.approve(widget.post.id) : await posts.reject(widget.post.id, comment: comment);
      messenger.show(approve ? 'Post został zatwierdzony.' : 'Post został odrzucony i zwrócony do poprawy.');
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
              if (post.status == PostStatus.rejected && post.rejectionComment != null)
                Padding(
                  padding: const EdgeInsets.symmetric(vertical: 4),
                  child: Text('Uwagi klienta: ${post.rejectionComment}',
                      key: const Key('rejection-comment'),
                      style: TextStyle(color: Theme.of(context).colorScheme.error)),
                ),
              if (post.canApprove)
                const Padding(
                  padding: EdgeInsets.symmetric(vertical: 4),
                  child: Text('Ten post czeka na Twoją akceptację przed publikacją.',
                      key: Key('awaiting-info')),
                ),
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

/// Okno uwag przy odrzuceniu posta. Kontroler tekstu należy do State, więc jest zwalniany dopiero po zniknięciu okna
/// (zwolnienie zaraz po zamknięciu dialogu powodowało użycie kontrolera po dispose podczas animacji zamykania).
class _RejectCommentDialog extends StatefulWidget {
  const _RejectCommentDialog();

  @override
  State<_RejectCommentDialog> createState() => _RejectCommentDialogState();
}

class _RejectCommentDialogState extends State<_RejectCommentDialog> {
  final _controller = TextEditingController();

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: const Text('Odrzuć post'),
      content: SizedBox(
        width: 420,
        child: TextField(
          key: const Key('reject-comment'),
          controller: _controller,
          autofocus: true,
          minLines: 3,
          maxLines: 6,
          maxLength: 1000,
          decoration: const InputDecoration(
            labelText: 'Uwagi i poprawki (opcjonalnie)',
            helperText: 'Post wróci do autorów z tymi uwagami.',
          ),
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.pop(context), child: const Text('Anuluj')),
        FilledButton(
          key: const Key('reject-confirm'),
          onPressed: () => Navigator.pop(context, _controller.text.trim()),
          child: const Text('Odrzuć i zwróć do poprawy'),
        ),
      ],
    );
  }
}

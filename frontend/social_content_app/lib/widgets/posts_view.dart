import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/enums.dart';
import '../models/post.dart';
import '../providers/load_state.dart';
import '../providers/post_provider.dart';
import '../providers/social_account_provider.dart';
import '../screens/post_form_screen.dart';
import '../services/api_exception.dart';
import '../utils/formatters.dart';
import 'async_body.dart';
import 'confirm_dialog.dart';

/// Lista postów z danego zakresu: [accountId] (posty konta) i/lub [managerId] (posty managera).
/// Bez żadnego z nich lista obejmuje wszystko, co API udostępnia roli (firma - posty całej firmy).
/// Edycję i usuwanie włączają flagi [Post.canEdit] i [Post.canDelete] liczone przez serwer
/// (firma: wszystkie posty swoich kont; manager: tylko na kontach, do których nadal jest przypisany).
class PostsView extends StatefulWidget {
  const PostsView({super.key, this.accountId, this.managerId});

  final String? accountId;
  final String? managerId;

  @override
  State<PostsView> createState() => _PostsViewState();
}

class _PostsViewState extends State<PostsView> {
  late final PostProvider _provider;

  LoadState _state = LoadState.idle;
  String? _error;
  List<Post> _items = const [];
  PostType? _filter;

  @override
  void initState() {
    super.initState();
    _provider = context.read<PostProvider>()..addListener(_load);
    _load();
  }

  @override
  void dispose() {
    _provider.removeListener(_load);
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _state = LoadState.loading;
      _error = null;
    });
    try {
      final items = await _provider.fetch(
        type: _filter,
        accountId: widget.accountId,
        managerId: widget.managerId,
      );
      if (!mounted) return;
      setState(() {
        _items = items;
        _state = LoadState.loaded;
      });
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _error = e.message;
        _state = LoadState.failed;
      });
    }
  }

  Future<void> _delete(Post p) async {
    if (!await confirmDelete(context, '"${p.title}"')) return;
    if (!mounted) return;
    final messenger = ScaffoldMessenger.of(context);
    try {
      await _provider.delete(p.id); // notifyListeners odświeża listę
    } on ApiException catch (e) {
      messenger.showSnackBar(SnackBar(content: Text(e.message)));
    }
  }

  Future<void> _open(Post p) async {
    if (p.lockedByOther) {
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(
          content: Text('Ten post jest właśnie edytowany przez: ${p.lock!.holderName}. '
              'Spróbuj ponownie za chwilę.')));
      return;
    }
    if (!p.canEdit) {
      await showDialog<void>(
        context: context,
        builder: (ctx) => _PostDetailsDialog(post: p),
      );
      return;
    }
    final accounts = context.read<SocialAccountProvider>().items;
    final account = accounts.where((a) => a.id == p.targetAccountId).firstOrNull;
    if (account == null) {
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(
          content: Text('Nie znaleziono konta tego posta - odśwież listę kont.')));
      return;
    }
    await Navigator.push(
      context,
      MaterialPageRoute(builder: (_) => PostFormScreen(account: account, post: p)),
    );
    if (mounted) _load(); // zmiana statusu/blokady bez zapisu też ma być widoczna
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        SizedBox(
          height: 52,
          child: ListView(
            scrollDirection: Axis.horizontal,
            padding: const EdgeInsets.symmetric(horizontal: 12),
            children: [
              _chip(null, 'Wszystkie'),
              for (final t in PostType.values) _chip(t, t.label),
            ],
          ),
        ),
        Expanded(
          child: AsyncBody(
            state: _state,
            error: _error,
            isEmpty: _items.isEmpty,
            emptyText: 'Brak postów.',
            onRetry: _load,
            builder: (_) => ListView.builder(
              padding: const EdgeInsets.fromLTRB(12, 4, 12, 96),
              itemCount: _items.length,
              itemBuilder: (context, i) => _PostCard(
                post: _items[i],
                onTap: () => _open(_items[i]),
                onDelete: _items[i].canDelete ? () => _delete(_items[i]) : null,
              ),
            ),
          ),
        ),
      ],
    );
  }

  Widget _chip(PostType? type, String label) => Padding(
        padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 8),
        child: ChoiceChip(
          label: Text(label),
          selected: _filter == type,
          onSelected: (_) {
            _filter = type;
            _load();
          },
        ),
      );
}

class _PostCard extends StatelessWidget {
  const _PostCard({required this.post, required this.onTap, this.onDelete});

  final Post post;
  final VoidCallback onTap;
  final VoidCallback? onDelete;

  IconData get _icon {
    switch (post.visibility) {
      case PostType.text:
        return Icons.notes;
      case PostType.photo:
        return Icons.image;
      case PostType.video:
        return Icons.videocam;
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card(
      child: InkWell(
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Icon(_icon, size: 20),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      post.title,
                      style: theme.textTheme.titleMedium,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                    ),
                  ),
                  _StatusChip(status: post.status),
                  if (onDelete != null)
                    IconButton(
                      tooltip: 'Usuń',
                      icon: const Icon(Icons.delete_outline),
                      onPressed: onDelete,
                    ),
                ],
              ),
              Text(post.description, maxLines: 3, overflow: TextOverflow.ellipsis),
              const SizedBox(height: 12),
              Wrap(
                spacing: 8,
                runSpacing: 4,
                children: [
                  Chip(
                    label: Text(post.visibility.label),
                    visualDensity: VisualDensity.compact,
                  ),
                  if (post.pictures.isNotEmpty)
                    Chip(
                      avatar: const Icon(Icons.image, size: 16),
                      label: Text('${post.pictures.length}'),
                      visualDensity: VisualDensity.compact,
                    ),
                  if (post.videos.isNotEmpty)
                    Chip(
                      avatar: const Icon(Icons.videocam, size: 16),
                      label: Text('${post.videos.length}'),
                      visualDensity: VisualDensity.compact,
                    ),
                ],
              ),
              const SizedBox(height: 8),
              Text(
                'Utworzono: ${formatDateTime(post.createdAt)}',
                style: theme.textTheme.bodySmall,
              ),
              if (post.status == PostStatus.scheduled && post.scheduledAt != null)
                Text('Zaplanowano na: ${formatDateTime(post.scheduledAt!)}',
                    style: theme.textTheme.bodySmall),
              if (post.status == PostStatus.published && post.publishedAt != null)
                Text('Opublikowano: ${formatDateTime(post.publishedAt!)}',
                    style: theme.textTheme.bodySmall),
              if (post.status == PostStatus.failed && post.errorMessage != null)
                Text('Błąd publikacji: ${post.errorMessage}',
                    style: theme.textTheme.bodySmall
                        ?.copyWith(color: theme.colorScheme.error)),
              if (post.managers.isNotEmpty)
                Text(
                  'Autorzy i współautorzy: ${post.managers.map((m) => m.fullName).join(', ')}',
                  style: theme.textTheme.bodySmall,
                ),
              if (!post.canEdit)
                Text(
                  post.status == PostStatus.published || post.status == PostStatus.publishing
                      ? 'Opublikowany post nie podlega edycji.'
                      : 'Tylko podgląd - brak uprawnień do edycji tego posta.',
                  style: theme.textTheme.bodySmall,
                ),
              if (post.lockedByOther)
                Text('Edytuje: ${post.lock!.holderName}',
                    style: theme.textTheme.bodySmall
                        ?.copyWith(color: theme.colorScheme.tertiary)),
            ],
          ),
        ),
      ),
    );
  }
}

/// Kolumna "Status" karty: opublikowany / zaplanowany / błąd (oraz szkic i publikowanie).
class _StatusChip extends StatelessWidget {
  const _StatusChip({required this.status});

  final PostStatus status;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final (IconData icon, Color color) = switch (status) {
      PostStatus.published => (Icons.check_circle, Colors.green),
      PostStatus.scheduled => (Icons.schedule, scheme.primary),
      PostStatus.failed => (Icons.error, scheme.error),
      PostStatus.publishing => (Icons.sync, scheme.tertiary),
      PostStatus.draft => (Icons.edit_note, scheme.outline),
    };
    return Chip(
      key: Key('status-${status.name}'),
      avatar: Icon(icon, size: 16, color: color),
      label: Text(status.label),
      visualDensity: VisualDensity.compact,
    );
  }
}

/// Podgląd posta (tylko do odczytu) - gdy użytkownik nie może go edytować (np. post opublikowany albo brak uprawnień).
class _PostDetailsDialog extends StatelessWidget {
  const _PostDetailsDialog({required this.post});

  final Post post;

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: Text(post.title),
      content: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(post.description),
            const SizedBox(height: 12),
            Text('Status: ${post.status.label}'),
            if (post.errorMessage != null) Text('Błąd: ${post.errorMessage}'),
            Text('Typ: ${post.visibility.label}'),
            Text('Zdjęcia: ${post.pictures.length}, wideo: ${post.videos.length}'),
            Text('Utworzono: ${formatDateTime(post.createdAt)}'),
            if (post.managers.isNotEmpty)
              Text('Autorzy i współautorzy: ${post.managers.map((m) => m.fullName).join(', ')}'),
          ],
        ),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(context),
          child: const Text('Zamknij'),
        ),
      ],
    );
  }
}

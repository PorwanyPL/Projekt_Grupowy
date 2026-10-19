import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/enums.dart';
import '../models/post.dart';
import '../providers/post_provider.dart';
import '../services/api_exception.dart';
import '../utils/formatters.dart';
import '../widgets/async_body.dart';
import '../widgets/confirm_dialog.dart';
import 'post_form_screen.dart';

class PostListScreen extends StatelessWidget {
  const PostListScreen({super.key});

  Future<void> _delete(BuildContext context, Post p) async {
    if (!await confirmDelete(context, '"${p.title}"')) return;
    if (!context.mounted) return;
    final provider = context.read<PostProvider>();
    final messenger = ScaffoldMessenger.of(context);
    try {
      await provider.delete(p.id);
    } on ApiException catch (e) {
      messenger.showSnackBar(SnackBar(content: Text(e.message)));
    }
  }

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<PostProvider>();

    return Scaffold(
      appBar: AppBar(
        title: const Text('Posty'),
        actions: [
          IconButton(
            tooltip: 'Odśwież',
            icon: const Icon(Icons.refresh),
            onPressed: provider.load,
          ),
        ],
        bottom: PreferredSize(
          preferredSize: const Size.fromHeight(52),
          child: SizedBox(
            height: 52,
            child: ListView(
              scrollDirection: Axis.horizontal,
              padding: const EdgeInsets.symmetric(horizontal: 12),
              children: [
                _chip(context, provider, null, 'Wszystkie'),
                for (final t in PostType.values)
                  _chip(context, provider, t, t.label),
              ],
            ),
          ),
        ),
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => Navigator.push(
          context,
          MaterialPageRoute(builder: (_) => const PostFormScreen()),
        ),
        icon: const Icon(Icons.add),
        label: const Text('Nowy post'),
      ),
      body: AsyncBody(
        state: provider.state,
        error: provider.error,
        isEmpty: provider.items.isEmpty,
        emptyText: 'Brak postów. Utwórz pierwszy.',
        onRetry: provider.load,
        builder: (_) => ListView.builder(
          padding: const EdgeInsets.fromLTRB(12, 12, 12, 96),
          itemCount: provider.items.length,
          itemBuilder: (context, i) => _PostCard(
            post: provider.items[i],
            onDelete: () => _delete(context, provider.items[i]),
          ),
        ),
      ),
    );
  }

  Widget _chip(
    BuildContext context,
    PostProvider provider,
    PostType? type,
    String label,
  ) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 8),
      child: ChoiceChip(
        label: Text(label),
        selected: provider.filter == type,
        onSelected: (_) => provider.setFilter(type),
      ),
    );
  }
}

class _PostCard extends StatelessWidget {
  const _PostCard({required this.post, required this.onDelete});

  final Post post;
  final VoidCallback onDelete;

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
        onTap: () => Navigator.push(
          context,
          MaterialPageRoute(builder: (_) => PostFormScreen(post: post)),
        ),
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
                  IconButton(
                    tooltip: 'Usuń',
                    icon: const Icon(Icons.delete_outline),
                    onPressed: onDelete,
                  ),
                ],
              ),
              Text(
                post.description,
                maxLines: 3,
                overflow: TextOverflow.ellipsis,
              ),
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
              if (post.managers.isNotEmpty)
                Text(
                  'Managerowie: ${post.managers.map((m) => m.fullName).join(', ')}',
                  style: theme.textTheme.bodySmall,
                ),
            ],
          ),
        ),
      ),
    );
  }
}

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/manager_user.dart';
import '../providers/manager_provider.dart';
import '../providers/post_provider.dart';
import '../services/api_exception.dart';
import '../widgets/async_body.dart';
import '../widgets/confirm_dialog.dart';
import 'manager_form_screen.dart';

class ManagerListScreen extends StatelessWidget {
  const ManagerListScreen({super.key});

  Future<void> _delete(BuildContext context, ManagerUser m) async {
    if (!await confirmDelete(context, m.fullName)) return;
    if (!context.mounted) return;
    final managers = context.read<ManagerProvider>();
    final posts = context.read<PostProvider>();
    final messenger = ScaffoldMessenger.of(context);
    try {
      await managers.delete(m.id);
      await posts.load(); // posty wskazywały na usuniętego managera
    } on ApiException catch (e) {
      messenger.showSnackBar(SnackBar(content: Text(e.message)));
    }
  }

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<ManagerProvider>();

    return Scaffold(
      appBar: AppBar(
        title: const Text('Managerowie'),
        actions: [
          IconButton(
            tooltip: 'Odśwież',
            icon: const Icon(Icons.refresh),
            onPressed: provider.load,
          ),
        ],
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => Navigator.push(
          context,
          MaterialPageRoute(builder: (_) => const ManagerFormScreen()),
        ),
        icon: const Icon(Icons.person_add),
        label: const Text('Nowy manager'),
      ),
      body: AsyncBody(
        state: provider.state,
        error: provider.error,
        isEmpty: provider.items.isEmpty,
        emptyText: 'Brak managerów. Dodaj pierwszego.',
        onRetry: provider.load,
        builder: (_) => ListView.separated(
          padding: const EdgeInsets.only(bottom: 96),
          itemCount: provider.items.length,
          separatorBuilder: (_, __) => const Divider(height: 1),
          itemBuilder: (context, i) {
            final m = provider.items[i];
            return ListTile(
              leading: CircleAvatar(
                child: Text(
                  '${m.firstName[0]}${m.lastName[0]}'.toUpperCase(),
                ),
              ),
              title: Text(m.fullName),
              subtitle: Text(
                '${m.age} lat · ${m.description}',
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
              ),
              trailing: IconButton(
                tooltip: 'Usuń',
                icon: const Icon(Icons.delete_outline),
                onPressed: () => _delete(context, m),
              ),
              onTap: () => Navigator.push(
                context,
                MaterialPageRoute(
                  builder: (_) => ManagerFormScreen(manager: m),
                ),
              ),
            );
          },
        ),
      ),
    );
  }
}

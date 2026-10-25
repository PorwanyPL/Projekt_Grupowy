import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/manager_user.dart';
import '../providers/auth_provider.dart';
import '../providers/manager_provider.dart';
import '../providers/post_provider.dart';
import '../providers/social_account_provider.dart';
import '../services/api_exception.dart';
import '../widgets/async_body.dart';
import '../widgets/confirm_dialog.dart';
import 'manager_form_screen.dart';

enum _ManagerAction { edit, block, unblock, anonymize, delete }

class ManagerListScreen extends StatelessWidget {
  const ManagerListScreen({super.key});

  Future<void> _onAction(
      BuildContext context, ManagerUser m, _ManagerAction action) async {
    switch (action) {
      case _ManagerAction.edit:
        await Navigator.push(
          context,
          MaterialPageRoute(builder: (_) => ManagerFormScreen(manager: m)),
        );
      case _ManagerAction.block:
        await _setBlocked(context, m, true);
      case _ManagerAction.unblock:
        await _setBlocked(context, m, false);
      case _ManagerAction.anonymize:
        await _anonymize(context, m);
      case _ManagerAction.delete:
        await _delete(context, m);
    }
  }

  Future<void> _setBlocked(BuildContext context, ManagerUser m, bool blocked) async {
    final managers = context.read<ManagerProvider>();
    final messenger = ScaffoldMessenger.of(context);
    try {
      await managers.setAccessBlocked(m.id, blocked);
      messenger.showSnackBar(SnackBar(
        content: Text(blocked
            ? 'Zablokowano dostęp: ${m.fullName}'
            : 'Odblokowano dostęp: ${m.fullName}'),
      ));
    } on ApiException catch (e) {
      messenger.showSnackBar(SnackBar(content: Text(e.message)));
    }
  }

  Future<void> _anonymize(BuildContext context, ManagerUser m) async {
    final ownId = context.read<AuthProvider>().profile?.id;
    final ok = await confirmAction(
      context,
      title: 'Zanonimizować managera?',
      message: 'Imię i nazwisko ${m.fullName} zostaną zastąpione napisem '
          '"Usunięty użytkownik", opis zostanie wyczyszczony, a konto logowania '
          'usunięte. Powiązania z postami zostaną zachowane.'
          '${ownId == m.id ? '\n\nTo Twoje konto - zostaniesz wylogowany.' : ''}'
          '\n\nTej operacji nie można cofnąć.',
      confirmLabel: 'Zanonimizuj',
    );
    if (!ok || !context.mounted) return;
    final managers = context.read<ManagerProvider>();
    final posts = context.read<PostProvider>();
    final accounts = context.read<SocialAccountProvider>();
    final messenger = ScaffoldMessenger.of(context);
    try {
      await managers.anonymize(m.id);
      await posts.load();
      await accounts.load();
      messenger.showSnackBar(
          const SnackBar(content: Text('Dane managera zostały zanonimizowane.')));
    } on ApiException catch (e) {
      messenger.showSnackBar(SnackBar(content: Text(e.message)));
    }
  }

  Future<void> _delete(BuildContext context, ManagerUser m) async {
    if (!await confirmDelete(context, m.fullName)) return;
    if (!context.mounted) return;
    final managers = context.read<ManagerProvider>();
    final posts = context.read<PostProvider>();
    final messenger = ScaffoldMessenger.of(context);
    try {
      await managers.delete(m.id);
      await posts.load();
    } on ApiException catch (e) {
      if (e.statusCode == 409 && context.mounted) {
        await _showDeleteConflict(context, m, e.message);
      } else {
        messenger.showSnackBar(SnackBar(content: Text(e.message)));
      }
    }
  }

  /// 409: manager ma powiązania - pokazujemy komunikat z API i proponujemy blokadę dostępu.
  Future<void> _showDeleteConflict(
      BuildContext context, ManagerUser m, String message) async {
    final canBlock = m.hasLoginAccount == true && m.isAccessBlocked != true;
    final block = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Nie można usunąć managera'),
        content: Text(
          canBlock
              ? message
              : '$message\n\nAby usunąć dane osobowe na prośbę użytkownika, '
                  'użyj opcji "Zanonimizuj".',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('Zamknij'),
          ),
          if (canBlock)
            FilledButton(
              onPressed: () => Navigator.pop(ctx, true),
              child: const Text('Zablokuj dostęp'),
            ),
        ],
      ),
    );
    if (block == true && context.mounted) {
      await _setBlocked(context, m, true);
    }
  }

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<ManagerProvider>();
    final ownId = context.watch<AuthProvider>().profile?.id;
    final scheme = Theme.of(context).colorScheme;

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
                '${m.age} lat · ${m.description}${_accessLabel(m)}',
                maxLines: 3,
                overflow: TextOverflow.ellipsis,
                style: m.isAccessBlocked == true
                    ? TextStyle(color: scheme.error)
                    : null,
              ),
              trailing: PopupMenuButton<_ManagerAction>(
                tooltip: 'Akcje',
                onSelected: (a) => _onAction(context, m, a),
                itemBuilder: (_) => [
                  const PopupMenuItem(
                    value: _ManagerAction.edit,
                    child: Text('Edytuj'),
                  ),
                  if (m.hasLoginAccount == true &&
                      m.isAccessBlocked != true &&
                      m.id != ownId)
                    const PopupMenuItem(
                      value: _ManagerAction.block,
                      child: Text('Zablokuj dostęp'),
                    ),
                  if (m.isAccessBlocked == true)
                    const PopupMenuItem(
                      value: _ManagerAction.unblock,
                      child: Text('Odblokuj dostęp'),
                    ),
                  const PopupMenuItem(
                    value: _ManagerAction.anonymize,
                    child: Text('Zanonimizuj dane'),
                  ),
                  const PopupMenuItem(
                    value: _ManagerAction.delete,
                    child: Text('Usuń'),
                  ),
                ],
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

String _accessLabel(ManagerUser m) {
  if (m.isAccessBlocked == true) return '\nDostęp zablokowany';
  if (m.hasLoginAccount == false) return '\nBez konta logowania';
  return '';
}

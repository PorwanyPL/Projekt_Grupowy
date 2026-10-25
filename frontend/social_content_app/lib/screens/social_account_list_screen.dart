import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/social_account.dart';
import '../providers/social_account_provider.dart';
import '../services/api_exception.dart';
import '../utils/formatters.dart';
import '../widgets/async_body.dart';
import '../widgets/confirm_dialog.dart';
import 'social_account_form_screen.dart';

class SocialAccountListScreen extends StatelessWidget {
  const SocialAccountListScreen({super.key});

  Future<void> _delete(BuildContext context, SocialAccount a) async {
    if (!await confirmDelete(context, '"${a.accountName}"')) return;
    if (!context.mounted) return;
    final provider = context.read<SocialAccountProvider>();
    final messenger = ScaffoldMessenger.of(context);
    try {
      await provider.delete(a.id);
    } on ApiException catch (e) {
      messenger.showSnackBar(SnackBar(content: Text(e.message)));
    }
  }

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<SocialAccountProvider>();
    final scheme = Theme.of(context).colorScheme;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Konta społecznościowe'),
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
          MaterialPageRoute(builder: (_) => const SocialAccountFormScreen()),
        ),
        icon: const Icon(Icons.add),
        label: const Text('Nowe konto'),
      ),
      body: AsyncBody(
        state: provider.state,
        error: provider.error,
        isEmpty: provider.items.isEmpty,
        emptyText: 'Brak kont społecznościowych. Dodaj pierwsze.',
        onRetry: provider.load,
        builder: (_) => ListView.separated(
          padding: const EdgeInsets.only(bottom: 96),
          itemCount: provider.items.length,
          separatorBuilder: (_, __) => const Divider(height: 1),
          itemBuilder: (context, i) {
            final a = provider.items[i];
            final expired = !a.tokenExpiresAt.isAfter(DateTime.now());
            return ListTile(
              leading: CircleAvatar(child: Text(a.platform.label[0])),
              title: Text(a.accountName),
              subtitle: Text(
                '${a.platform.label} · ${a.isActive ? 'aktywne' : 'nieaktywne'}\n'
                'Token ${expired ? 'wygasł' : 'ważny do'}: ${formatDateTime(a.tokenExpiresAt)}'
                '${a.managers.isEmpty ? '' : '\nManagerowie: ${a.managers.map((m) => m.fullName).join(', ')}'}'
                '${a.errorMessage == null ? '' : '\nBłąd: ${a.errorMessage}'}',
                style: expired || a.errorMessage != null
                    ? TextStyle(color: scheme.error)
                    : null,
              ),
              isThreeLine: true,
              trailing: IconButton(
                tooltip: 'Usuń',
                icon: const Icon(Icons.delete_outline),
                onPressed: () => _delete(context, a),
              ),
              onTap: () => Navigator.push(
                context,
                MaterialPageRoute(
                  builder: (_) => SocialAccountFormScreen(account: a),
                ),
              ),
            );
          },
        ),
      ),
    );
  }
}

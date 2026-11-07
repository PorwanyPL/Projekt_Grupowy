import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/social_account.dart';
import '../providers/auth_provider.dart';
import '../providers/social_account_provider.dart';
import '../utils/formatters.dart';
import '../widgets/account_menu.dart';
import '../widgets/posts_view.dart';
import 'assign_managers_screen.dart';
import 'post_form_screen.dart';
import 'social_account_form_screen.dart';

/// Widok szczegółowy konta społecznościowego. Posty tworzy tu manager przypisany do konta oraz firma
/// (firma może też tworzyć posty z zakładki Posty). Firma przypisuje managerów i edytuje konto.
class SocialAccountDetailScreen extends StatelessWidget {
  const SocialAccountDetailScreen({super.key, required this.accountId});

  final String accountId;

  @override
  Widget build(BuildContext context) {
    final accounts = context.watch<SocialAccountProvider>().items;
    final account = accounts.where((a) => a.id == accountId).firstOrNull;
    final profile = context.watch<AuthProvider>().profile;

    if (account == null || profile == null) {
      return Scaffold(
        appBar: AppBar(title: const Text('Konto')),
        body: const Center(child: Text('To konto nie jest już dostępne.')),
      );
    }

    final isCompany = profile.isCompany;
    final canCreatePosts = isCompany || (profile.isManager && account.hasManager(profile.id));
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(
        title: Text(account.accountName),
        actions: [
          if (isCompany)
            IconButton(
              tooltip: 'Edytuj konto',
              icon: const Icon(Icons.edit_outlined),
              onPressed: () => Navigator.push(
                context,
                MaterialPageRoute(
                  builder: (_) => SocialAccountFormScreen(account: account),
                ),
              ),
            ),
          ...accountMenuActions(context),
        ],
      ),
      floatingActionButton: canCreatePosts
          ? FloatingActionButton.extended(
              onPressed: () => Navigator.push(
                context,
                MaterialPageRoute(builder: (_) => PostFormScreen(account: account)),
              ),
              icon: const Icon(Icons.add),
              label: const Text('Nowy post'),
            )
          : null,
      body: Column(
        children: [
          _AccountInfo(account: account, isCompany: isCompany),
          const Divider(),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16),
            child: Align(
              alignment: Alignment.centerLeft,
              child: Text(
                isCompany ? 'Posty konta' : 'Moje posty na tym koncie',
                style: theme.textTheme.titleSmall,
              ),
            ),
          ),
          Expanded(
            child: PostsView(accountId: account.id),
          ),
        ],
      ),
    );
  }
}

class _AccountInfo extends StatelessWidget {
  const _AccountInfo({required this.account, required this.isCompany});

  final SocialAccount account;
  final bool isCompany;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final expired = !account.tokenExpiresAt.isAfter(DateTime.now());

    return Card(
      margin: const EdgeInsets.all(12),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('${account.platform.label} · ${account.accountId}'),
            Text(account.isActive ? 'Konto aktywne' : 'Konto nieaktywne'),
            Text(
              'Token ${expired ? 'wygasł' : 'ważny do'}: ${formatDateTime(account.tokenExpiresAt)}',
              style: expired ? TextStyle(color: scheme.error) : null,
            ),
            if (account.errorMessage != null)
              Text('Błąd: ${account.errorMessage}',
                  style: TextStyle(color: scheme.error)),
            const SizedBox(height: 12),
            Row(
              children: [
                Text('Managerowie', style: Theme.of(context).textTheme.titleSmall),
                const Spacer(),
                if (isCompany)
                  TextButton.icon(
                    onPressed: () => Navigator.push(
                      context,
                      MaterialPageRoute(
                        builder: (_) => AssignManagersScreen(account: account),
                      ),
                    ),
                    icon: const Icon(Icons.group_add),
                    label: const Text('Przypisz'),
                  ),
              ],
            ),
            if (account.managers.isEmpty)
              const Text('Brak przypisanych managerów - nikt nie może tworzyć postów.')
            else
              Wrap(
                spacing: 8,
                runSpacing: 4,
                children: [
                  for (final m in account.managers)
                    Chip(
                      avatar: const Icon(Icons.person, size: 16),
                      label: Text(m.fullName),
                      visualDensity: VisualDensity.compact,
                    ),
                ],
              ),
          ],
        ),
      ),
    );
  }
}

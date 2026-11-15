import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/social_account.dart';
import '../providers/auth_provider.dart';
import '../providers/social_account_provider.dart';
import '../services/api_exception.dart';
import '../utils/formatters.dart';
import '../utils/validators.dart';
import '../widgets/app_messenger.dart';
import '../widgets/auto_refresh.dart';
import '../widgets/account_menu.dart';
import '../widgets/posts_view.dart';
import 'assign_managers_screen.dart';
import 'post_form_screen.dart';
import 'social_account_form_screen.dart';

/// Widok szczegółowy profilu społecznościowego.
///  - klient (właściciel): dane profilu, stan zaproszenia firmy, wszystkie posty konta TYLKO do odczytu
///    (z podglądem i zatwierdzaniem, gdy firma wymaga jego akceptacji),
///  - firma: przypisuje managerów (po przyjęciu zaproszenia) i tworzy posty,
///  - manager: tworzy i edytuje posty na przypisanym koncie.
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
    final isClient = profile.isClient;
    final canCreatePosts = (isCompany && account.isAccepted) ||
        (profile.isManager && account.hasManager(profile.id));
    final theme = Theme.of(context);
    final provider = context.read<SocialAccountProvider>();

    return AutoRefresh(
      onRefresh: () => provider.load(silent: true),
      child: Scaffold(
      appBar: AppBar(
        title: Text(account.accountName),
        actions: [
          if (isClient)
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
          _AccountInfo(account: account, isCompany: isCompany, isClient: isClient),
          const Divider(),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16),
            child: Align(
              alignment: Alignment.centerLeft,
              child: Text(
                isCompany || isClient ? 'Posty konta' : 'Moje posty na tym koncie',
                style: theme.textTheme.titleSmall,
              ),
            ),
          ),
          Expanded(
            child: PostsView(accountId: account.id),
          ),
        ],
      ),
    ));
  }
}

class _AccountInfo extends StatelessWidget {
  const _AccountInfo({required this.account, required this.isCompany, required this.isClient});

  final SocialAccount account;
  final bool isCompany;
  final bool isClient;

  Future<void> _invite(BuildContext context) async {
    final controller = TextEditingController();
    final formKey = GlobalKey<FormState>();
    final email = await showDialog<String>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Zaproś firmę'),
        content: Form(
          key: formKey,
          child: TextFormField(
            controller: controller,
            autofocus: true,
            keyboardType: TextInputType.emailAddress,
            decoration: const InputDecoration(labelText: 'E-mail firmy'),
            validator: FormValidators.email,
          ),
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('Anuluj')),
          FilledButton(
            onPressed: () {
              if (formKey.currentState!.validate()) Navigator.pop(ctx, controller.text.trim());
            },
            child: const Text('Zaproś'),
          ),
        ],
      ),
    );
    controller.dispose();
    if (email == null || !context.mounted) return;
    final provider = context.read<SocialAccountProvider>();
    final messenger = context.read<AppMessenger>();
    try {
      await provider.invite(account.id, email);
    } on ApiException catch (e) {
      messenger.show(e.message);
    }
  }

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
            if (!isClient && account.ownerName.isNotEmpty) Text('Klient: ${account.ownerName}'),
            Text(account.isActive ? 'Konto aktywne' : 'Konto nieaktywne'),
            Row(
              children: [
                Expanded(
                  child: Text(
                    'Firma: ${account.companyName.isEmpty ? '-' : account.companyName} · ${account.managementStatus.label}',
                    key: const Key('account-company-status'),
                  ),
                ),
                if (isClient && !account.isAccepted)
                  TextButton(
                    key: const Key('invite-company'),
                    onPressed: () => _invite(context),
                    child: const Text('Zaproś firmę'),
                  ),
              ],
            ),
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
                if (isCompany && account.isAccepted)
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
              Text(account.isAccepted
                  ? 'Brak przypisanych managerów - nikt nie może tworzyć postów.'
                  : 'Managerów można przypisać po przyjęciu zaproszenia.')
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

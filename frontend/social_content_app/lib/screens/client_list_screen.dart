import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/client_user.dart';
import '../providers/auth_provider.dart';
import '../providers/client_provider.dart';
import '../providers/social_account_provider.dart';
import '../services/api_exception.dart';
import '../widgets/account_menu.dart';
import '../widgets/app_messenger.dart';
import '../widgets/async_body.dart';
import '../widgets/auto_refresh.dart';
import '../widgets/confirm_dialog.dart';
import 'client_accounts_screen.dart';
import 'client_link_screen.dart';

/// Zakładka "Konta" firmy i managera: najpierw lista klientów (kafelek z nazwą/aliasem nadanym przez firmę),
/// po kliknięciu - profile tego klienta obsługiwane przez firmę. Manager widzi tylko klientów, których profile
/// ma przypisane. Firma może dodatkowo powiązać klienta po e-mailu, nadać mu alias i go odpiąć.
class ClientListScreen extends StatelessWidget {
  const ClientListScreen({super.key});

  Future<void> _unlink(BuildContext context, ClientUser c) async {
    final ok = await confirmAction(
      context,
      title: 'Odpiąć klienta?',
      message: '${c.title} przestanie być klientem Twojej firmy. '
          'Nie można odpiąć klienta, który ma zaakceptowane profile - najpierw usuń lub odrzuć jego profile.',
      confirmLabel: 'Odepnij',
    );
    if (!ok || !context.mounted) return;
    final provider = context.read<ClientProvider>();
    final messenger = context.read<AppMessenger>();
    try {
      await provider.unlink(c.id);
    } on ApiException catch (e) {
      messenger.show(e.message);
    }
  }

  Future<void> _editAlias(BuildContext context, ClientUser c) async {
    final controller = TextEditingController(text: c.alias ?? '');
    final alias = await showDialog<String>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Alias klienta'),
        content: TextField(
          controller: controller,
          maxLength: 100,
          autofocus: true,
          decoration: InputDecoration(
            labelText: 'Nazwa widoczna w firmie',
            helperText: 'Klient: ${c.fullName}. Puste pole usuwa alias.',
          ),
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('Anuluj')),
          FilledButton(onPressed: () => Navigator.pop(ctx, controller.text.trim()), child: const Text('Zapisz')),
        ],
      ),
    );
    controller.dispose();
    if (alias == null || !context.mounted) return;
    final provider = context.read<ClientProvider>();
    final accounts = context.read<SocialAccountProvider>();
    final messenger = context.read<AppMessenger>();
    try {
      await provider.setAlias(c.id, alias.isEmpty ? null : alias);
      await accounts.load(silent: true); // nazwa właściciela na profilach też się zmienia
    } on ApiException catch (e) {
      messenger.show(e.message);
    }
  }

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<ClientProvider>();
    final isCompany = context.watch<AuthProvider>().profile?.isCompany ?? false;
    final accounts = context.read<SocialAccountProvider>();

    return AutoRefresh(
      onRefresh: () {
        provider.loadClients(silent: true);
        accounts.load(silent: true);
      },
      child: Scaffold(
        appBar: AppBar(
          title: const Text('Klienci'),
          actions: [
            IconButton(
              tooltip: 'Odśwież',
              icon: const Icon(Icons.refresh),
              onPressed: () {
                provider.loadClients();
                accounts.load(silent: true);
              },
            ),
            ...accountMenuActions(context),
          ],
        ),
        floatingActionButton: isCompany
            ? FloatingActionButton.extended(
                onPressed: () => Navigator.push(
                  context,
                  MaterialPageRoute(builder: (_) => const ClientLinkScreen()),
                ),
                icon: const Icon(Icons.person_add),
                label: const Text('Dodaj klienta'),
              )
            : null,
        body: AsyncBody(
          state: provider.state,
          error: provider.error,
          isEmpty: provider.clients.isEmpty,
          emptyText: isCompany
              ? 'Brak klientów. Dodaj klienta po e-mailu albo poczekaj na zaproszenie do jego profilu.'
              : 'Nie jesteś przypisany do żadnego profilu. Poproś administratora firmy.',
          onRetry: provider.loadClients,
          builder: (_) => ListView.separated(
            padding: const EdgeInsets.only(bottom: 96),
            itemCount: provider.clients.length,
            separatorBuilder: (_, __) => const Divider(height: 1),
            itemBuilder: (context, i) {
              final c = provider.clients[i];
              final initials = c.title.isEmpty ? '?' : c.title[0].toUpperCase();
              return ListTile(
                key: Key('client-${c.id}'),
                leading: CircleAvatar(child: Text(initials)),
                title: Text(c.title),
                subtitle: Text([
                  if (c.alias != null) c.fullName,
                  if (isCompany && c.email.isNotEmpty) c.email,
                ].join(' · ')),
                trailing: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    if (isCompany && c.pendingInvitations > 0)
                      Badge(
                        key: Key('pending-${c.id}'),
                        label: Text('${c.pendingInvitations}'),
                        child: const Icon(Icons.mail_outline),
                      ),
                    if (isCompany)
                      PopupMenuButton<String>(
                        tooltip: 'Więcej',
                        onSelected: (v) => v == 'alias' ? _editAlias(context, c) : _unlink(context, c),
                        itemBuilder: (_) => const [
                          PopupMenuItem(value: 'alias', child: Text('Zmień alias')),
                          PopupMenuItem(value: 'unlink', child: Text('Odepnij klienta')),
                        ],
                      )
                    else
                      const Icon(Icons.chevron_right),
                  ],
                ),
                onTap: () => Navigator.push(
                  context,
                  MaterialPageRoute(builder: (_) => ClientAccountsScreen(client: c)),
                ),
              );
            },
          ),
        ),
      ),
    );
  }
}

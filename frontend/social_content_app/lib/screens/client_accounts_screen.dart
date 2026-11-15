import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/client_user.dart';
import '../models/social_account.dart';
import '../providers/auth_provider.dart';
import '../providers/social_account_provider.dart';
import '../services/api_exception.dart';
import '../utils/formatters.dart';
import '../widgets/account_menu.dart';
import '../widgets/app_messenger.dart';
import '../widgets/async_body.dart';
import '../widgets/auto_refresh.dart';
import 'social_account_detail_screen.dart';

/// Firma / manager: profile wybranego klienta obsługiwane przez tę firmę. Firma widzi też zaproszenia
/// do obsługi nowych profili klienta i może je przyjąć albo odrzucić.
class ClientAccountsScreen extends StatelessWidget {
  const ClientAccountsScreen({super.key, required this.client});

  final ClientUser client;

  Future<void> _decide(BuildContext context, SocialAccount a, {required bool accept}) async {
    final provider = context.read<SocialAccountProvider>();
    final messenger = context.read<AppMessenger>();
    try {
      accept ? await provider.accept(a.id) : await provider.decline(a.id);
    } on ApiException catch (e) {
      messenger.show(e.message);
    }
  }

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<SocialAccountProvider>();
    final isCompany = context.watch<AuthProvider>().profile?.isCompany ?? false;
    final scheme = Theme.of(context).colorScheme;
    // Firma nie widzi odrzuconych zaproszeń (serwer ich nie zwraca); lista = profile tego klienta.
    final items = provider.items.where((a) => a.ownerClientId == client.id).toList();

    return AutoRefresh(
      onRefresh: () => provider.load(silent: true),
      child: Scaffold(
        appBar: AppBar(
          title: Text(client.title),
          actions: accountMenuActions(context),
        ),
        body: AsyncBody(
          state: provider.state,
          error: provider.error,
          isEmpty: items.isEmpty,
          emptyText: 'Brak profili tego klienta obsługiwanych przez Twoją firmę.',
          onRetry: provider.load,
          builder: (_) => ListView.separated(
            padding: const EdgeInsets.only(bottom: 24),
            itemCount: items.length,
            separatorBuilder: (_, __) => const Divider(height: 1),
            itemBuilder: (context, i) {
              final a = items[i];
              final expired = !a.tokenExpiresAt.isAfter(DateTime.now());
              final pending = a.isInvited;
              return Column(
                children: [
                  ListTile(
                    key: Key('account-${a.id}'),
                    leading: CircleAvatar(child: Text(a.platform.label[0])),
                    title: Text(a.accountName),
                    subtitle: Text(
                      '${a.platform.label} · ${a.isActive ? 'aktywne' : 'nieaktywne'} · ${a.managementStatus.label}\n'
                      'Token ${expired ? 'wygasł' : 'ważny do'}: ${formatDateTime(a.tokenExpiresAt)}'
                      '${a.managers.isEmpty ? '' : '\nManagerowie: ${a.managers.map((m) => m.fullName).join(', ')}'}'
                      '${a.errorMessage == null ? '' : '\nBłąd: ${a.errorMessage}'}',
                      style: expired || a.errorMessage != null ? TextStyle(color: scheme.error) : null,
                    ),
                    isThreeLine: true,
                    trailing: pending ? null : const Icon(Icons.chevron_right),
                    onTap: pending
                        ? null
                        : () => Navigator.push(
                              context,
                              MaterialPageRoute(
                                  builder: (_) => SocialAccountDetailScreen(accountId: a.id)),
                            ),
                  ),
                  if (pending && isCompany)
                    Padding(
                      padding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
                      child: Row(
                        mainAxisAlignment: MainAxisAlignment.end,
                        children: [
                          OutlinedButton(
                            key: Key('decline-${a.id}'),
                            onPressed: () => _decide(context, a, accept: false),
                            child: const Text('Odrzuć'),
                          ),
                          const SizedBox(width: 8),
                          FilledButton(
                            key: Key('accept-${a.id}'),
                            onPressed: () => _decide(context, a, accept: true),
                            child: const Text('Przyjmij zaproszenie'),
                          ),
                        ],
                      ),
                    ),
                ],
              );
            },
          ),
        ),
      ),
    );
  }
}

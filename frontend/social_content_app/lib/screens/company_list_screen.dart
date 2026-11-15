import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/enums.dart';
import '../providers/client_provider.dart';
import '../providers/social_account_provider.dart';
import '../widgets/account_menu.dart';
import '../widgets/async_body.dart';
import '../widgets/auto_refresh.dart';
import 'client_company_screen.dart';

/// ClientUser: firmy, z którymi klient jest powiązany (relacja n - m). Kliknięcie otwiera widok firmy:
/// wybór trybu zatwierdzania postów i profile obsługiwane przez tę firmę.
class CompanyListScreen extends StatelessWidget {
  const CompanyListScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<ClientProvider>();
    final accounts = context.read<SocialAccountProvider>();

    return AutoRefresh(
      onRefresh: () {
        provider.loadCompanies(silent: true);
        accounts.load(silent: true);
      },
      child: Scaffold(
        appBar: AppBar(
          title: const Text('Moje firmy'),
          actions: [
            IconButton(
              tooltip: 'Odśwież',
              icon: const Icon(Icons.refresh),
              onPressed: provider.loadCompanies,
            ),
            ...accountMenuActions(context),
          ],
        ),
        body: AsyncBody(
          state: provider.state,
          error: provider.error,
          isEmpty: provider.companies.isEmpty,
          emptyText: 'Żadna firma nie obsługuje jeszcze Twoich profili. '
              'Dodaj profil w zakładce "Moje profile" i zaproś firmę adresem e-mail.',
          onRetry: provider.loadCompanies,
          builder: (_) => ListView.separated(
            itemCount: provider.companies.length,
            separatorBuilder: (_, __) => const Divider(height: 1),
            itemBuilder: (context, i) {
              final c = provider.companies[i];
              return ListTile(
                key: Key('company-${c.companyId}'),
                leading: const CircleAvatar(child: Icon(Icons.business)),
                title: Text(c.name),
                subtitle: Text(c.approvalMode == PostApprovalMode.client
                    ? 'Posty zatwierdzasz Ty'
                    : 'Posty zatwierdza manager firmy'),
                trailing: const Icon(Icons.chevron_right),
                onTap: () => Navigator.push(
                  context,
                  MaterialPageRoute(builder: (_) => ClientCompanyScreen(companyId: c.companyId)),
                ),
              );
            },
          ),
        ),
      ),
    );
  }
}

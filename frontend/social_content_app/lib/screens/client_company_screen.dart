import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/enums.dart';
import '../providers/client_provider.dart';
import '../providers/social_account_provider.dart';
import '../services/api_exception.dart';
import '../widgets/account_menu.dart';
import '../widgets/app_messenger.dart';
import '../widgets/auto_refresh.dart';
import 'social_account_detail_screen.dart';

/// ClientUser: widok jednej firmy - wybór, kto zatwierdza posty przed publikacją (klient czy manager firmy),
/// oraz lista profili klienta obsługiwanych przez tę firmę (wejście = wszystkie posty konta, tylko do odczytu).
class ClientCompanyScreen extends StatelessWidget {
  const ClientCompanyScreen({super.key, required this.companyId});

  final String companyId;

  Future<void> _setMode(BuildContext context, PostApprovalMode mode) async {
    final provider = context.read<ClientProvider>();
    final messenger = context.read<AppMessenger>();
    try {
      await provider.setApprovalMode(companyId, mode);
      await context.read<SocialAccountProvider>().load(silent: true);
    } on ApiException catch (e) {
      messenger.show(e.message);
    }
  }

  @override
  Widget build(BuildContext context) {
    final clients = context.watch<ClientProvider>();
    final accounts = context.watch<SocialAccountProvider>();
    final company = clients.companies.where((c) => c.companyId == companyId).firstOrNull;
    final items = accounts.items.where((a) => a.companyId == companyId && a.isAccepted).toList();

    if (company == null) {
      return Scaffold(
        appBar: AppBar(title: const Text('Firma'), actions: accountMenuActions(context)),
        body: const Center(child: Text('Ta firma nie jest już dostępna.')),
      );
    }

    return AutoRefresh(
      onRefresh: () {
        clients.loadCompanies(silent: true);
        accounts.load(silent: true);
      },
      child: Scaffold(
        appBar: AppBar(title: Text(company.name), actions: accountMenuActions(context)),
        body: ListView(
          children: [
            const Padding(
              padding: EdgeInsets.fromLTRB(16, 16, 16, 0),
              child: Text('Zatwierdzanie postów przed publikacją',
                  style: TextStyle(fontWeight: FontWeight.w600)),
            ),
            RadioListTile<PostApprovalMode>(
              key: const Key('approval-client'),
              value: PostApprovalMode.client,
              groupValue: company.approvalMode,
              title: const Text('Zatwierdzam posty sam'),
              subtitle: const Text('Posty tej firmy trafią do Ciebie do akceptacji, zanim zostaną opublikowane.'),
              onChanged: (v) => _setMode(context, v!),
            ),
            RadioListTile<PostApprovalMode>(
              key: const Key('approval-manager'),
              value: PostApprovalMode.manager,
              groupValue: company.approvalMode,
              title: const Text('Deleguję decyzję managerowi firmy'),
              subtitle: const Text('Firma publikuje posty bez Twojej akceptacji.'),
              onChanged: (v) => _setMode(context, v!),
            ),
            const Divider(),
            const Padding(
              padding: EdgeInsets.fromLTRB(16, 8, 16, 0),
              child: Text('Profile obsługiwane przez firmę', style: TextStyle(fontWeight: FontWeight.w600)),
            ),
            if (items.isEmpty)
              const Padding(
                padding: EdgeInsets.all(16),
                child: Text('Ta firma nie obsługuje jeszcze żadnego z Twoich profili.'),
              ),
            for (final a in items)
              ListTile(
                key: Key('account-${a.id}'),
                leading: CircleAvatar(child: Text(a.platform.label[0])),
                title: Text(a.accountName),
                subtitle: Text('${a.platform.label} · ${a.isActive ? 'aktywne' : 'nieaktywne'}'),
                trailing: const Icon(Icons.chevron_right),
                onTap: () => Navigator.push(
                  context,
                  MaterialPageRoute(builder: (_) => SocialAccountDetailScreen(accountId: a.id)),
                ),
              ),
          ],
        ),
      ),
    );
  }
}

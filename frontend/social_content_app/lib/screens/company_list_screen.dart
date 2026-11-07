import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../providers/client_provider.dart';
import '../widgets/account_menu.dart';
import '../widgets/async_body.dart';

/// ClientUser: firmy, z którymi klient jest powiązany (relacja n - m).
class CompanyListScreen extends StatelessWidget {
  const CompanyListScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<ClientProvider>();

    return Scaffold(
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
        emptyText: 'Żadna firma jeszcze Cię nie dodała. '
            'Podaj firmie adres e-mail użyty przy rejestracji.',
        onRetry: provider.loadCompanies,
        builder: (_) => ListView.separated(
          itemCount: provider.companies.length,
          separatorBuilder: (_, __) => const Divider(height: 1),
          itemBuilder: (context, i) {
            final c = provider.companies[i];
            return ListTile(
              leading: const CircleAvatar(child: Icon(Icons.business)),
              title: Text(c.name),
            );
          },
        ),
      ),
    );
  }
}

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/client_user.dart';
import '../providers/client_provider.dart';
import '../services/api_exception.dart';
import '../widgets/account_menu.dart';
import '../widgets/async_body.dart';
import '../widgets/confirm_dialog.dart';
import 'client_link_screen.dart';

/// CompanyUser: klienci powiązani z firmą (relacja n - m - ten sam klient może należeć do wielu firm).
class ClientListScreen extends StatelessWidget {
  const ClientListScreen({super.key});

  Future<void> _unlink(BuildContext context, ClientUser c) async {
    final ok = await confirmAction(
      context,
      title: 'Odpiąć klienta?',
      message: '${c.fullName} przestanie być klientem Twojej firmy. '
          'Jego konto i powiązania z innymi firmami zostaną bez zmian.',
      confirmLabel: 'Odepnij',
    );
    if (!ok || !context.mounted) return;
    final provider = context.read<ClientProvider>();
    final messenger = ScaffoldMessenger.of(context);
    try {
      await provider.unlink(c.id);
    } on ApiException catch (e) {
      messenger.showSnackBar(SnackBar(content: Text(e.message)));
    }
  }

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<ClientProvider>();

    return Scaffold(
      appBar: AppBar(
        title: const Text('Klienci'),
        actions: [
          IconButton(
            tooltip: 'Odśwież',
            icon: const Icon(Icons.refresh),
            onPressed: provider.loadClients,
          ),
          ...accountMenuActions(context),
        ],
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => Navigator.push(
          context,
          MaterialPageRoute(builder: (_) => const ClientLinkScreen()),
        ),
        icon: const Icon(Icons.person_add),
        label: const Text('Dodaj klienta'),
      ),
      body: AsyncBody(
        state: provider.state,
        error: provider.error,
        isEmpty: provider.clients.isEmpty,
        emptyText: 'Brak klientów. Klient rejestruje się sam - dodaj go po adresie e-mail.',
        onRetry: provider.loadClients,
        builder: (_) => ListView.separated(
          padding: const EdgeInsets.only(bottom: 96),
          itemCount: provider.clients.length,
          separatorBuilder: (_, __) => const Divider(height: 1),
          itemBuilder: (context, i) {
            final c = provider.clients[i];
            return ListTile(
              leading: CircleAvatar(
                child: Text('${c.firstName[0]}${c.lastName[0]}'.toUpperCase()),
              ),
              title: Text(c.fullName),
              subtitle: Text('${c.email}\n${c.age} lat · ${c.description}',
                  maxLines: 3, overflow: TextOverflow.ellipsis),
              isThreeLine: true,
              trailing: IconButton(
                tooltip: 'Odepnij klienta',
                icon: const Icon(Icons.link_off),
                onPressed: () => _unlink(context, c),
              ),
            );
          },
        ),
      ),
    );
  }
}

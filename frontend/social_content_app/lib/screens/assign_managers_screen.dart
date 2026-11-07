import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/social_account.dart';
import '../providers/manager_provider.dart';
import '../providers/social_account_provider.dart';
import '../services/api_exception.dart';
import '../widgets/async_body.dart';

/// CompanyUser: wybiera managerów uprawnionych do tworzenia postów na danym koncie.
/// Zapisana lista zastępuje poprzednią (relacja n - m: manager może obsługiwać wiele kont).
class AssignManagersScreen extends StatefulWidget {
  const AssignManagersScreen({super.key, required this.account});

  final SocialAccount account;

  @override
  State<AssignManagersScreen> createState() => _AssignManagersScreenState();
}

class _AssignManagersScreenState extends State<AssignManagersScreen> {
  late final Set<String> _selected;
  bool _saving = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _selected = {...widget.account.managers.map((m) => m.id)};
  }

  Future<void> _save() async {
    setState(() {
      _saving = true;
      _error = null;
    });
    final accounts = context.read<SocialAccountProvider>();
    final managers = context.read<ManagerProvider>();
    try {
      await accounts.assignManagers(widget.account.id, _selected.toList());
      await managers.load(); // przypisania są widoczne też na liście managerów
      if (mounted) Navigator.pop(context);
    } on ApiException catch (e) {
      if (mounted) {
        setState(() {
          _saving = false;
          _error = e.message;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<ManagerProvider>();
    final scheme = Theme.of(context).colorScheme;

    return Scaffold(
      appBar: AppBar(title: Text('Managerowie: ${widget.account.accountName}')),
      body: Column(
        children: [
          Expanded(
            child: AsyncBody(
              state: provider.state,
              error: provider.error,
              isEmpty: provider.items.isEmpty,
              emptyText: 'Najpierw utwórz managerów w zakładce "Managerowie".',
              onRetry: provider.load,
              builder: (_) => ListView(
                children: [
                  const Padding(
                    padding: EdgeInsets.all(16),
                    child: Text(
                      'Zaznaczeni managerowie mogą tworzyć posty na tym koncie. '
                      'Odznaczenie odbiera uprawnienia (ich dotychczasowe posty zostają).',
                    ),
                  ),
                  for (final m in provider.items)
                    CheckboxListTile(
                      value: _selected.contains(m.id),
                      title: Text(m.fullName),
                      subtitle: Text(m.hasLoginAccount == true
                          ? (m.isAccessBlocked == true
                              ? 'Dostęp zablokowany'
                              : 'Ma konto logowania')
                          : 'Bez konta logowania - nie zaloguje się'),
                      onChanged: _saving
                          ? null
                          : (on) => setState(() {
                                on == true
                                    ? _selected.add(m.id)
                                    : _selected.remove(m.id);
                              }),
                    ),
                ],
              ),
            ),
          ),
          if (_error != null)
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16),
              child: Text(_error!, style: TextStyle(color: scheme.error)),
            ),
          SafeArea(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: SizedBox(
                width: double.infinity,
                child: FilledButton(
                  onPressed: _saving ? null : _save,
                  child: _saving
                      ? const SizedBox(
                          height: 20,
                          width: 20,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Text('Zapisz przypisania'),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

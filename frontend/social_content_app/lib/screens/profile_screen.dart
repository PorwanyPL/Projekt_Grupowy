import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../providers/auth_provider.dart';
import '../services/api_exception.dart';
import '../widgets/confirm_dialog.dart';

class ProfileScreen extends StatelessWidget {
  const ProfileScreen({super.key});

  Future<void> _logout(BuildContext context) async {
    final ok = await confirmAction(
      context,
      title: 'Wylogować?',
      message: 'Zostaniesz wylogowany z aplikacji.',
      confirmLabel: 'Wyloguj',
    );
    if (!ok || !context.mounted) return;
    await context.read<AuthProvider>().logout();
  }

  Future<void> _toggleTwoFactor(BuildContext context, bool enable) async {
    final password = await showDialog<String>(
      context: context,
      builder: (_) => _PasswordDialog(enable: enable),
    );
    if (password == null || !context.mounted) return;
    final messenger = ScaffoldMessenger.of(context);
    try {
      await context.read<AuthProvider>().setTwoFactor(enable, password);
      messenger.showSnackBar(SnackBar(
          content: Text(enable ? 'Weryfikacja dwuetapowa włączona.' : 'Weryfikacja dwuetapowa wyłączona.')));
    } on ApiException catch (e) {
      messenger.showSnackBar(SnackBar(content: Text(e.message)));
    }
  }

  @override
  Widget build(BuildContext context) {
    final profile = context.watch<AuthProvider>().profile;

    return Scaffold(
      appBar: AppBar(title: const Text('Profil')),
      body: profile == null
          ? const Center(child: CircularProgressIndicator())
          : ListView(
              padding: const EdgeInsets.all(16),
              children: [
                ListTile(
                  leading: CircleAvatar(
                    child: Icon(profile.isCompany ? Icons.business : Icons.person),
                  ),
                  title: Text(profile.displayName),
                  subtitle: Text(profile.email),
                ),
                const Divider(),
                ListTile(
                  title: const Text('Rola'),
                  subtitle: Text(profile.role.label),
                ),
                if (profile.age != null)
                  ListTile(
                    title: const Text('Wiek'),
                    subtitle: Text('${profile.age}'),
                  ),
                if (profile.description != null)
                  ListTile(
                    title: const Text('Opis'),
                    subtitle: Text(profile.description!),
                  ),
                const Divider(),
                SwitchListTile(
                  key: const Key('two-factor-switch'),
                  title: const Text('Weryfikacja dwuetapowa'),
                  subtitle: const Text('Przy logowaniu wyślemy kod na Twój adres e-mail.'),
                  value: profile.twoFactorEnabled,
                  onChanged: (v) => _toggleTwoFactor(context, v),
                ),
                const SizedBox(height: 24),
                OutlinedButton.icon(
                  onPressed: () => _logout(context),
                  icon: const Icon(Icons.logout),
                  label: const Text('Wyloguj'),
                ),
              ],
            ),
    );
  }
}

/// Pyta o hasło (potwierdzenie zmiany 2FA). Kontroler należy do tego widgetu i jest zwalniany w dispose.
class _PasswordDialog extends StatefulWidget {
  const _PasswordDialog({required this.enable});

  final bool enable;

  @override
  State<_PasswordDialog> createState() => _PasswordDialogState();
}

class _PasswordDialogState extends State<_PasswordDialog> {
  final _controller = TextEditingController();

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: Text(widget.enable ? 'Włączyć weryfikację dwuetapową?' : 'Wyłączyć weryfikację dwuetapową?'),
      content: TextField(
        key: const Key('two-factor-password'),
        controller: _controller,
        autofocus: true,
        obscureText: true,
        decoration: const InputDecoration(labelText: 'Potwierdź hasłem'),
        onSubmitted: (v) => Navigator.pop(context, v),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.pop(context), child: const Text('Anuluj')),
        FilledButton(
          key: const Key('two-factor-confirm'),
          onPressed: () => Navigator.pop(context, _controller.text),
          child: const Text('Potwierdź'),
        ),
      ],
    );
  }
}

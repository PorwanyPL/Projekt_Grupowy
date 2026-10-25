import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../providers/auth_provider.dart';
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
                  leading: const CircleAvatar(child: Icon(Icons.person)),
                  title: Text(profile.fullName),
                  subtitle: Text(profile.email),
                ),
                const Divider(),
                ListTile(
                  title: const Text('Wiek'),
                  subtitle: Text('${profile.age}'),
                ),
                ListTile(
                  title: const Text('Opis'),
                  subtitle: Text(profile.description),
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

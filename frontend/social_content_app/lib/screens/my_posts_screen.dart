import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/social_account.dart';
import '../providers/auth_provider.dart';
import '../providers/social_account_provider.dart';
import 'post_form_screen.dart';
import '../widgets/account_menu.dart';
import '../widgets/posts_view.dart';

/// Manager: jego posty (jako autora lub współautora). Edycja i usuwanie tylko na kontach, do których nadal jest
/// przypisany - po utracie uprawnień posty są widoczne tylko do przeglądania.
class MyPostsScreen extends StatelessWidget {
  const MyPostsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final id = context.watch<AuthProvider>().profile?.id;
    return Scaffold(
      appBar: AppBar(title: const Text('Moje posty'), actions: accountMenuActions(context)),
      body: id == null
          ? const SizedBox.shrink()
          : PostsView(managerId: id),
    );
  }
}

/// Firma (widok admina): wszystkie posty jej kont oraz tworzenie nowych na KAŻDYM koncie firmy,
/// niezależnie od przypisanych managerów - tym samym formularzem co manager.
class CompanyPostsScreen extends StatelessWidget {
  const CompanyPostsScreen({super.key});

  Future<void> _newPost(BuildContext context) async {
    final accounts = context.read<SocialAccountProvider>().items;
    if (accounts.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Najpierw dodaj konto społecznościowe.')));
      return;
    }
    final account = await showModalBottomSheet<SocialAccount>(
      context: context,
      builder: (ctx) => SafeArea(
        child: ListView(
          shrinkWrap: true,
          children: [
            const ListTile(title: Text('Na którym koncie utworzyć post?')),
            for (final a in accounts)
              ListTile(
                leading: const Icon(Icons.share),
                title: Text(a.accountName),
                subtitle: Text(a.platform.label),
                onTap: () => Navigator.pop(ctx, a),
              ),
          ],
        ),
      ),
    );
    if (account == null || !context.mounted) return;
    await Navigator.push(
        context, MaterialPageRoute(builder: (_) => PostFormScreen(account: account)));
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Posty firmy'), actions: accountMenuActions(context)),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => _newPost(context),
        icon: const Icon(Icons.add),
        label: const Text('Nowy post'),
      ),
      body: const PostsView(),
    );
  }
}

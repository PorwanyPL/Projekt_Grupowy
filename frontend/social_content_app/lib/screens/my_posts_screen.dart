import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../providers/auth_provider.dart';
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

/// Firma (widok admina): lista wszystkich postów jej kont z filtrami oraz tworzenie nowych - formularz sam pozwala
/// wybrać klienta i platformy (dowolne profile firmy, niezależnie od przypisanych managerów).
class CompanyPostsScreen extends StatelessWidget {
  const CompanyPostsScreen({super.key});

  Future<void> _newPost(BuildContext context) => Navigator.push(
        context,
        MaterialPageRoute(builder: (_) => const PostFormScreen()),
      );

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

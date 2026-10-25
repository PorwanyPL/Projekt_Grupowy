import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../providers/manager_provider.dart';
import '../providers/post_provider.dart';
import '../providers/social_account_provider.dart';
import 'manager_list_screen.dart';
import 'post_list_screen.dart';
import 'profile_screen.dart';
import 'social_account_list_screen.dart';

class HomeScreen extends StatefulWidget {
  const HomeScreen({super.key});

  @override
  State<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends State<HomeScreen> {
  int _index = 0;

  @override
  void initState() {
    super.initState();
    // Dane pobieramy dopiero po zalogowaniu - wszystkie endpointy wymagają tokenu.
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      context.read<ManagerProvider>().load();
      context.read<PostProvider>().load();
      context.read<SocialAccountProvider>().load();
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: IndexedStack(
        index: _index,
        children: const [
          PostListScreen(),
          ManagerListScreen(),
          SocialAccountListScreen(),
          ProfileScreen(),
        ],
      ),
      bottomNavigationBar: NavigationBar(
        selectedIndex: _index,
        onDestinationSelected: (i) => setState(() => _index = i),
        destinations: const [
          NavigationDestination(
            icon: Icon(Icons.article_outlined),
            selectedIcon: Icon(Icons.article),
            label: 'Posty',
          ),
          NavigationDestination(
            icon: Icon(Icons.people_outline),
            selectedIcon: Icon(Icons.people),
            label: 'Managerowie',
          ),
          NavigationDestination(
            icon: Icon(Icons.share_outlined),
            selectedIcon: Icon(Icons.share),
            label: 'Konta',
          ),
          NavigationDestination(
            icon: Icon(Icons.account_circle_outlined),
            selectedIcon: Icon(Icons.account_circle),
            label: 'Profil',
          ),
        ],
      ),
    );
  }
}

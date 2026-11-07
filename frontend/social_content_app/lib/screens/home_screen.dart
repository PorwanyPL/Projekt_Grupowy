import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/auth.dart';
import '../providers/auth_provider.dart';
import '../providers/client_provider.dart';
import '../providers/manager_provider.dart';
import '../providers/social_account_provider.dart';
import 'client_list_screen.dart';
import 'company_list_screen.dart';
import 'manager_list_screen.dart';
import 'my_posts_screen.dart';
import 'social_account_list_screen.dart';

class _Tab {
  const _Tab(this.label, this.icon, this.selectedIcon, this.screen);

  final String label;
  final IconData icon;
  final IconData selectedIcon;
  final Widget screen;
}

/// Ekran główny: zestaw zakładek zależy od roli zalogowanego użytkownika.
///  - CompanyUser: Konta, Posty, Managerowie, Klienci
///  - ManagerUser: Konta (tylko przypisane), Moje posty
///  - ClientUser: Firmy
/// Profil, powiadomienia i wylogowanie są w górnym menu każdego ekranu (widgets/account_menu.dart).
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
      switch (context.read<AuthProvider>().role) {
        case UserRole.companyUser:
          context.read<SocialAccountProvider>().load();
          context.read<ManagerProvider>().load();
          context.read<ClientProvider>().loadClients();
        case UserRole.managerUser:
          context.read<SocialAccountProvider>().load();
        case UserRole.clientUser:
          context.read<ClientProvider>().loadCompanies();
        case null:
          break;
      }
    });
  }

  List<_Tab> _tabsFor(UserRole? role) {
    switch (role) {
      case UserRole.companyUser:
        return const [
          _Tab('Konta', Icons.share_outlined, Icons.share, SocialAccountListScreen()),
          _Tab('Posty', Icons.article_outlined, Icons.article, CompanyPostsScreen()),
          _Tab('Managerowie', Icons.people_outline, Icons.people, ManagerListScreen()),
          _Tab('Klienci', Icons.groups_outlined, Icons.groups, ClientListScreen()),
        ];
      case UserRole.managerUser:
        return const [
          _Tab('Konta', Icons.share_outlined, Icons.share, SocialAccountListScreen()),
          _Tab('Moje posty', Icons.article_outlined, Icons.article, MyPostsScreen()),
        ];
      case UserRole.clientUser:
        return const [
          _Tab('Firmy', Icons.business_outlined, Icons.business, CompanyListScreen()),
        ];
      case null:
        return const [];
    }
  }

  @override
  Widget build(BuildContext context) {
    final tabs = _tabsFor(context.watch<AuthProvider>().role);
    if (tabs.isEmpty) return const Scaffold(body: Center(child: CircularProgressIndicator()));
    final index = _index.clamp(0, tabs.length - 1);

    return Scaffold(
      body: IndexedStack(
        index: index,
        children: [for (final t in tabs) t.screen],
      ),
      // NavigationBar wymaga min. 2 pozycji; klient ma jedną (Firmy), więc dolnego paska nie pokazujemy.
      bottomNavigationBar: tabs.length < 2
          ? null
          : NavigationBar(
        selectedIndex: index,
        onDestinationSelected: (i) => setState(() => _index = i),
        destinations: [
          for (final t in tabs)
            NavigationDestination(
              icon: Icon(t.icon),
              selectedIcon: Icon(t.selectedIcon),
              label: t.label,
            ),
        ],
      ),
    );
  }
}

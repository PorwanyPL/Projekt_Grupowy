import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/auth.dart';
import '../providers/auth_provider.dart';
import '../providers/client_provider.dart';
import '../providers/manager_provider.dart';
import '../providers/social_account_provider.dart';
import 'calendar_screen.dart';
import 'client_list_screen.dart';
import '../widgets/auto_refresh.dart';
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
///  - CompanyUser: Konta (lista klientów -> ich profile), Kalendarz, Posty (lista z filtrami), Managerowie
///  - ManagerUser: Konta (klienci z przypisanymi profilami), Kalendarz, Moje posty
///  - ClientUser: Moje profile, Kalendarz (tylko podgląd i akceptacja), Firmy
/// Każda zakładka ma własny Navigator, więc dolne menu jest widoczne na każdym ekranie, a górne (profil,
/// powiadomienia, wylogowanie - widgets/account_menu.dart) jest w AppBarze każdego ekranu.
class HomeScreen extends StatefulWidget {
  const HomeScreen({super.key});

  @override
  State<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends State<HomeScreen> {
  int _index = 0;

  /// Osobny Navigator dla każdej zakładki (maks. 4), żeby ekrany szczegółowe otwierały się
  /// pod dolnym menu, a nie na całym ekranie.
  final List<GlobalKey<NavigatorState>> _navKeys =
      List.generate(4, (_) => GlobalKey<NavigatorState>());

  /// Każdy Navigator ma własnego obserwatora tras (współdzielony powodował błąd asercji w Flutterze).
  final List<RouteObserver<ModalRoute<void>>> _observers =
      List.generate(4, (_) => RouteObserver<ModalRoute<void>>());

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
          context.read<ClientProvider>().loadClients();
        case UserRole.clientUser:
          context.read<SocialAccountProvider>().load();
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
          _Tab('Konta', Icons.share_outlined, Icons.share, ClientListScreen()),
          _Tab('Kalendarz', Icons.calendar_month_outlined, Icons.calendar_month, CalendarScreen()),
          _Tab('Posty', Icons.article_outlined, Icons.article, CompanyPostsScreen()),
          _Tab('Managerowie', Icons.people_outline, Icons.people, ManagerListScreen()),
        ];
      case UserRole.managerUser:
        return const [
          _Tab('Konta', Icons.share_outlined, Icons.share, ClientListScreen()),
          _Tab('Kalendarz', Icons.calendar_month_outlined, Icons.calendar_month, CalendarScreen()),
          _Tab('Moje posty', Icons.article_outlined, Icons.article, MyPostsScreen()),
        ];
      case UserRole.clientUser:
        return const [
          _Tab('Moje profile', Icons.share_outlined, Icons.share, SocialAccountListScreen()),
          _Tab('Kalendarz', Icons.calendar_month_outlined, Icons.calendar_month, CalendarScreen()),
          _Tab('Firmy', Icons.business_outlined, Icons.business, CompanyListScreen()),
        ];
      case null:
        return const [];
    }
  }

  void _selectTab(int i, int current) {
    final nav = _navKeys[i].currentState;
    if (i == current) {
      nav?.popUntil((r) => r.isFirst); // ponowne dotknięcie aktywnej zakładki wraca do jej listy
    } else {
      setState(() => _index = i);
    }
    context.read<RefreshBus>().notifyTabChanged();
  }

  @override
  Widget build(BuildContext context) {
    final tabs = _tabsFor(context.watch<AuthProvider>().role);
    if (tabs.isEmpty) return const Scaffold(body: Center(child: CircularProgressIndicator()));
    final index = _index.clamp(0, tabs.length - 1);

    return PopScope(
      // Przycisk "wstecz" najpierw cofa ekran w aktywnej zakładce.
      canPop: false,
      onPopInvokedWithResult: (didPop, _) {
        if (didPop) return;
        final nav = _navKeys[index].currentState;
        if (nav != null && nav.canPop()) nav.pop();
      },
      child: Scaffold(
        body: IndexedStack(
          index: index,
          children: [
            for (var i = 0; i < tabs.length; i++)
              RouteObserverScope(
                observer: _observers[i],
                child: Navigator(
                  key: _navKeys[i],
                  observers: [_observers[i]],
                  onGenerateRoute: (_) => MaterialPageRoute<void>(builder: (_) => tabs[i].screen),
                ),
              ),
          ],
        ),
        bottomNavigationBar: NavigationBar(
          selectedIndex: index,
          onDestinationSelected: (i) => _selectTab(i, index),
          destinations: [
            for (final t in tabs)
              NavigationDestination(
                icon: Icon(t.icon),
                selectedIcon: Icon(t.selectedIcon),
                label: t.label,
              ),
          ],
        ),
      ),
    );
  }
}

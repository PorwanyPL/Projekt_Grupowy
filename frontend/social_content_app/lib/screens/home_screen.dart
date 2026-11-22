import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/app_notification.dart';
import '../models/auth.dart';
import '../providers/auth_provider.dart';
import '../providers/chat_provider.dart';
import '../providers/client_provider.dart';
import '../providers/home_navigation.dart';
import '../providers/notification_provider.dart';
import '../widgets/app_messenger.dart';
import '../providers/manager_provider.dart';
import '../providers/social_account_provider.dart';
import 'calendar_screen.dart';
import 'chat_list_screen.dart';
import 'chat_screen.dart';
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

/// Ekran główny (każda rola ma też zakładkę Czat): zestaw zakładek zależy od roli zalogowanego użytkownika.
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
  late final ChatProvider _chat;
  late final HomeNavigation _navigation;

  /// Osobny Navigator dla każdej zakładki (maks. 5), żeby ekrany szczegółowe otwierały się
  /// pod dolnym menu, a nie na całym ekranie.
  final List<GlobalKey<NavigatorState>> _navKeys =
      List.generate(5, (_) => GlobalKey<NavigatorState>());

  /// Każdy Navigator ma własnego obserwatora tras (współdzielony powodował błąd asercji w Flutterze).
  final List<RouteObserver<ModalRoute<void>>> _observers =
      List.generate(5, (_) => RouteObserver<ModalRoute<void>>());

  @override
  void initState() {
    super.initState();
    _chat = context.read<ChatProvider>();
    _navigation = context.read<HomeNavigation>()..addListener(_onNavigationRequest);
    // Dane pobieramy dopiero po zalogowaniu - wszystkie endpointy wymagają tokenu.
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      _startChat();
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

  @override
  void dispose() {
    _navigation.removeListener(_onNavigationRequest);
    // Wylogowanie zamyka HomeScreen: zatrzymujemy połączenie SignalR (po zakończeniu budowania drzewa).
    Future.microtask(_chat.stop);
    super.dispose();
  }

  /// Kliknięcie powiadomienia o wiadomości: przełącza na zakładkę Czat i otwiera rozmowę nad jej listą.
  void _onNavigationRequest() {
    final id = _navigation.takePendingConversation();
    if (id == null || !mounted) return;
    final tabs = _tabsFor(context.read<AuthProvider>().role);
    final chatIndex = tabs.indexWhere((t) => t.label == 'Czat');
    if (chatIndex < 0) return;
    setState(() => _index = chatIndex);
    final nav = _navKeys[chatIndex].currentState;
    nav?.popUntil((r) => r.isFirst);
    nav?.push(MaterialPageRoute<void>(builder: (_) => ChatScreen(conversationId: id)));
  }

  /// Czat i powiadomienia na żywo: jedno połączenie SignalR na sesję. Nowa wiadomość = krótki komunikat
  /// (AppMessenger), nowe powiadomienie o poście trafia od razu do dzwonka.
  void _startChat() {
    final profile = context.read<AuthProvider>().profile;
    if (profile == null) return;
    final chat = context.read<ChatProvider>();
    final messenger = context.read<AppMessenger>();
    final notifications = context.read<NotificationProvider>();
    chat.onIncoming = (text) => messenger.show('Nowa wiadomość - $text');
    chat.onNotification = (n) {
      notifications.push(n);
      // Wiadomości czatu zapowiada już ChatProvider.onIncoming (z treścią), więc nie dublujemy komunikatu.
      if (n.targetType != NotificationTarget.chat) messenger.show(n.message);
    };
    chat.start(profile.id, profile.role);
  }

  List<_Tab> _tabsFor(UserRole? role) {
    switch (role) {
      case UserRole.companyUser:
        return const [
          _Tab('Konta', Icons.share_outlined, Icons.share, ClientListScreen()),
          _Tab('Kalendarz', Icons.calendar_month_outlined, Icons.calendar_month, CalendarScreen()),
          _Tab('Posty', Icons.article_outlined, Icons.article, CompanyPostsScreen()),
          _Tab('Managerowie', Icons.people_outline, Icons.people, ManagerListScreen()),
          _Tab('Czat', Icons.chat_bubble_outline, Icons.chat_bubble, ChatListScreen()),
        ];
      case UserRole.managerUser:
        return const [
          _Tab('Konta', Icons.share_outlined, Icons.share, ClientListScreen()),
          _Tab('Kalendarz', Icons.calendar_month_outlined, Icons.calendar_month, CalendarScreen()),
          _Tab('Moje posty', Icons.article_outlined, Icons.article, MyPostsScreen()),
          _Tab('Czat', Icons.chat_bubble_outline, Icons.chat_bubble, ChatListScreen()),
        ];
      case UserRole.clientUser:
        return const [
          _Tab('Moje profile', Icons.share_outlined, Icons.share, SocialAccountListScreen()),
          _Tab('Kalendarz', Icons.calendar_month_outlined, Icons.calendar_month, CalendarScreen()),
          _Tab('Firmy', Icons.business_outlined, Icons.business, CompanyListScreen()),
          _Tab('Czat', Icons.chat_bubble_outline, Icons.chat_bubble, ChatListScreen()),
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

  Widget _tabIcon(_Tab t, bool selected, int unreadChat) {
    final icon = Icon(selected ? t.selectedIcon : t.icon);
    if (t.label != 'Czat' || unreadChat == 0) return icon;
    return Badge(label: Text('$unreadChat'), child: icon);
  }

  @override
  Widget build(BuildContext context) {
    final tabs = _tabsFor(context.watch<AuthProvider>().role);
    if (tabs.isEmpty) return const Scaffold(body: Center(child: CircularProgressIndicator()));
    final index = _index.clamp(0, tabs.length - 1);
    final unreadChat = context.watch<ChatProvider>().totalUnread;

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
                icon: _tabIcon(t, false, unreadChat),
                selectedIcon: _tabIcon(t, true, unreadChat),
                label: t.label,
              ),
          ],
        ),
      ),
    );
  }
}

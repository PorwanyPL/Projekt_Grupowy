import 'dart:async';

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/auth.dart';
import '../providers/auth_provider.dart';
import '../providers/notification_provider.dart';
import '../screens/profile_screen.dart';
import '../utils/formatters.dart';
import 'confirm_dialog.dart';

/// Wspólne górne menu wszystkich ról (dzwonek powiadomień i menu profilu z danymi, rolą, profilem i wylogowaniem).
/// Użycie: `AppBar(actions: [...accountMenuActions(context), ...])`.
List<Widget> accountMenuActions(BuildContext context) => const [
      NotificationBell(),
      AccountMenuButton(),
    ];

enum _MenuAction { profile, logout }

/// Menu konta: u góry nazwa, e-mail i rola, poniżej "Profil" i "Wyloguj".
class AccountMenuButton extends StatelessWidget {
  const AccountMenuButton({super.key});

  Future<void> _onSelected(BuildContext context, _MenuAction action) async {
    switch (action) {
      case _MenuAction.profile:
        await Navigator.push(
            context, MaterialPageRoute(builder: (_) => const ProfileScreen()));
      case _MenuAction.logout:
        final ok = await confirmAction(
          context,
          title: 'Wylogować?',
          message: 'Zostaniesz wylogowany z aplikacji.',
          confirmLabel: 'Wyloguj',
        );
        if (!ok || !context.mounted) return;
        await context.read<AuthProvider>().logout();
    }
  }

  @override
  Widget build(BuildContext context) {
    final profile = context.watch<AuthProvider>().profile;
    if (profile == null) return const SizedBox.shrink();

    return PopupMenuButton<_MenuAction>(
      tooltip: 'Konto: ${profile.displayName}',
      icon: CircleAvatar(
        radius: 14,
        child: Icon(profile.isCompany ? Icons.business : Icons.person, size: 16),
      ),
      onSelected: (a) => _onSelected(context, a),
      itemBuilder: (ctx) => [
        PopupMenuItem<_MenuAction>(
          enabled: false,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(profile.displayName,
                  style: Theme.of(ctx).textTheme.titleSmall?.copyWith(
                      color: Theme.of(ctx).colorScheme.onSurface)),
              Text(profile.email, style: Theme.of(ctx).textTheme.bodySmall),
              Text(profile.role.label, style: Theme.of(ctx).textTheme.bodySmall),
            ],
          ),
        ),
        const PopupMenuDivider(),
        const PopupMenuItem(
          value: _MenuAction.profile,
          child: ListTile(
            dense: true,
            contentPadding: EdgeInsets.zero,
            leading: Icon(Icons.account_circle_outlined),
            title: Text('Profil'),
          ),
        ),
        const PopupMenuItem(
          value: _MenuAction.logout,
          child: ListTile(
            dense: true,
            contentPadding: EdgeInsets.zero,
            leading: Icon(Icons.logout),
            title: Text('Wyloguj'),
          ),
        ),
      ],
    );
  }
}

/// Dzwonek powiadomień (firma i manager; klient ich nie dostaje). Odświeża się co minutę.
class NotificationBell extends StatefulWidget {
  const NotificationBell({super.key});

  @override
  State<NotificationBell> createState() => _NotificationBellState();
}

class _NotificationBellState extends State<NotificationBell> {
  Timer? _timer;

  bool get _enabled {
    final role = context.read<AuthProvider>().role;
    return role == UserRole.companyUser || role == UserRole.managerUser;
  }

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted || !_enabled) return;
      context.read<NotificationProvider>().load();
      _timer = Timer.periodic(const Duration(minutes: 1), (_) {
        if (mounted) context.read<NotificationProvider>().load();
      });
    });
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  Future<void> _open() async {
    final provider = context.read<NotificationProvider>();
    await provider.load();
    if (!mounted) return;
    await showDialog<void>(
      context: context,
      builder: (_) => _NotificationsDialog(provider: provider),
    );
    await provider.markAllRead();
  }

  @override
  Widget build(BuildContext context) {
    final role = context.watch<AuthProvider>().role;
    if (role != UserRole.companyUser && role != UserRole.managerUser) {
      return const SizedBox.shrink();
    }
    final unread = context.watch<NotificationProvider>().unread;
    return IconButton(
      tooltip: 'Powiadomienia',
      onPressed: _open,
      icon: Badge(
        isLabelVisible: unread > 0,
        label: Text('$unread'),
        child: const Icon(Icons.notifications_outlined),
      ),
    );
  }
}

class _NotificationsDialog extends StatelessWidget {
  const _NotificationsDialog({required this.provider});

  final NotificationProvider provider;

  @override
  Widget build(BuildContext context) {
    final items = provider.items;
    return AlertDialog(
      title: const Text('Powiadomienia'),
      content: SizedBox(
        width: 420,
        child: items.isEmpty
            ? const Text('Brak powiadomień.')
            : ListView(
                shrinkWrap: true,
                children: [
                  for (final n in items)
                    ListTile(
                      leading: Icon(
                        Icons.error_outline,
                        color: n.isRead ? null : Theme.of(context).colorScheme.error,
                      ),
                      title: Text(n.message,
                          style: n.isRead
                              ? null
                              : const TextStyle(fontWeight: FontWeight.w600)),
                      subtitle: Text(formatDateTime(n.createdAt)),
                    ),
                ],
              ),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(context),
          child: const Text('Zamknij'),
        ),
      ],
    );
  }
}

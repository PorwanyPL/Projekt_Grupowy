import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/auth.dart';
import '../models/enums.dart';
import '../models/manager_user.dart';
import '../models/post.dart';
import '../models/post_filter.dart';
import '../providers/auth_provider.dart';
import '../providers/load_state.dart';
import '../providers/post_provider.dart';
import '../providers/social_account_provider.dart';
import '../services/api_exception.dart';
import '../widgets/account_menu.dart';
import '../widgets/app_messenger.dart';
import '../widgets/auto_refresh.dart';
import '../widgets/platform_icons.dart';
import '../widgets/post_actions.dart';
import '../widgets/post_filter_dialog.dart';
import 'post_form_screen.dart';

const _monthNames = [
  'Styczeń', 'Luty', 'Marzec', 'Kwiecień', 'Maj', 'Czerwiec',
  'Lipiec', 'Sierpień', 'Wrzesień', 'Październik', 'Listopad', 'Grudzień',
];
const _weekdayNames = ['Pon', 'Wt', 'Śr', 'Czw', 'Pt', 'Sob', 'Nd'];

/// Kalendarz postów - pierwszy ekran tworzenia i planowania. Kliknięcie dnia otwiera formularz nowego posta
/// z terminem publikacji ustawionym na ten dzień (można go zmienić); kliknięcie posta otwiera jego edycję tymi samymi
/// zasadami co lista (blokada edycji, podgląd tylko do odczytu dla klienta). Widoczność postów ustala serwer według roli.
/// Filtry (status, osoba, tagi, profile i platformy) łączą się koniunkcją.
class CalendarScreen extends StatefulWidget {
  const CalendarScreen({super.key});

  @override
  State<CalendarScreen> createState() => _CalendarScreenState();
}

class _CalendarScreenState extends State<CalendarScreen> {
  late final PostProvider _provider;
  DateTime _month = DateTime(DateTime.now().year, DateTime.now().month);
  PostFilter _filter = PostFilter.empty;
  LoadState _state = LoadState.idle;
  String? _error;
  List<Post> _posts = const [];

  @override
  void initState() {
    super.initState();
    _provider = context.read<PostProvider>()..addListener(_onProviderChanged);
    _load();
  }

  @override
  void dispose() {
    _provider.removeListener(_onProviderChanged);
    super.dispose();
  }

  void _onProviderChanged() => _load(silent: true);

  /// Pierwszy (poniedziałek) i ostatni widoczny dzień siatki miesiąca.
  (DateTime, DateTime) get _range {
    final first = DateTime(_month.year, _month.month, 1);
    final start = first.subtract(Duration(days: first.weekday - 1));
    final last = DateTime(_month.year, _month.month + 1, 0);
    final end = last.add(Duration(days: 7 - last.weekday));
    return (start, end);
  }

  Future<void> _load({bool silent = false}) async {
    final keep = silent && _state == LoadState.loaded;
    if (!keep) {
      setState(() {
        _state = LoadState.loading;
        _error = null;
      });
    }
    final (start, end) = _range;
    try {
      final posts = await _provider.fetch(
        filter: _filter.copyWith(
          from: start,
          to: DateTime(end.year, end.month, end.day, 23, 59, 59),
        ),
      );
      if (!mounted) return;
      setState(() {
        _posts = posts;
        _state = LoadState.loaded;
      });
    } on ApiException catch (e) {
      if (!mounted || keep) return;
      setState(() {
        _error = e.message;
        _state = LoadState.failed;
      });
    }
  }

  void _shift(int months) {
    setState(() => _month = DateTime(_month.year, _month.month + months));
    _load();
  }

  void _today() {
    final n = DateTime.now();
    setState(() => _month = DateTime(n.year, n.month));
    _load();
  }

  bool get _canCreate {
    final role = context.read<AuthProvider>().role;
    return role == UserRole.companyUser || role == UserRole.managerUser;
  }

  List<ManagerUser> get _managers {
    final seen = <String>{};
    return [
      for (final a in context.read<SocialAccountProvider>().items)
        for (final m in a.managers)
          if (seen.add(m.id)) m,
    ]..sort((a, b) => a.fullName.compareTo(b.fullName));
  }

  Future<void> _editFilters() async {
    final accounts = context.read<SocialAccountProvider>().items.where((a) => a.isAccepted).toList();
    final result = await showPostFilterDialog(
      context,
      initial: _filter,
      managers: _managers,
      tags: _provider.knownTags.toList(),
      accounts: accounts,
    );
    if (result == null || !mounted) return;
    setState(() => _filter = result);
    _load();
  }

  Future<void> _create(DateTime day) async {
    if (!_canCreate) return;
    final today = DateTime.now();
    if (DateTime(day.year, day.month, day.day).isBefore(DateTime(today.year, today.month, today.day))) {
      context.read<AppMessenger>().show('Nie można zaplanować posta w przeszłości - wybierz dzisiejszy lub przyszły dzień.');
      return;
    }
    await Navigator.push(
      context,
      MaterialPageRoute(builder: (_) => PostFormScreen(initialDate: day)),
    );
    if (mounted) _load(silent: true);
  }

  Future<void> _openPost(Post p) async {
    final changed = await openPost(context, p);
    if (changed && mounted) _load(silent: true);
  }

  Future<void> _showDay(DateTime day, List<Post> posts) => showDialog<void>(
        context: context,
        builder: (ctx) => AlertDialog(
          title: Text('${day.day}.${day.month.toString().padLeft(2, '0')}.${day.year}'),
          content: SizedBox(
            width: 380,
            child: ListView(
              shrinkWrap: true,
              children: [
                for (final p in posts)
                  Padding(
                    padding: const EdgeInsets.only(bottom: 6),
                    child: SizedBox(
                      height: 56,
                      child: CalendarPostTile(
                        post: p,
                        onTap: () {
                          Navigator.pop(ctx);
                          _openPost(p);
                        },
                      ),
                    ),
                  ),
              ],
            ),
          ),
          actions: [
            if (_canCreate)
              TextButton(
                onPressed: () {
                  Navigator.pop(ctx);
                  _create(day);
                },
                child: const Text('Nowy post tego dnia'),
              ),
            TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('Zamknij')),
          ],
        ),
      );

  @override
  Widget build(BuildContext context) {
    final (start, end) = _range;
    final days = end.difference(start).inDays + 1;
    final byDay = <DateTime, List<Post>>{};
    for (final p in _posts) {
      final d = p.calendarDate;
      byDay.putIfAbsent(DateTime(d.year, d.month, d.day), () => []).add(p);
    }
    for (final list in byDay.values) {
      list.sort((a, b) => a.calendarDate.compareTo(b.calendarDate));
    }
    final today = DateTime.now();

    return AutoRefresh(
      onRefresh: () => _load(silent: true),
      child: Scaffold(
        appBar: AppBar(title: const Text('Kalendarz'), actions: accountMenuActions(context)),
        floatingActionButton: _canCreate
            ? FloatingActionButton.extended(
                key: const Key('calendar-create'),
                onPressed: () => _create(today),
                icon: const Icon(Icons.add),
                label: const Text('Utwórz post'),
              )
            : null,
        body: Column(
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(8, 8, 8, 0),
              child: Row(
                children: [
                  IconButton(
                      key: const Key('calendar-prev'),
                      tooltip: 'Poprzedni miesiąc',
                      icon: const Icon(Icons.chevron_left),
                      onPressed: () => _shift(-1)),
                  Expanded(
                    child: Text(
                      '${_monthNames[_month.month - 1]} ${_month.year}',
                      key: const Key('calendar-title'),
                      textAlign: TextAlign.center,
                      style: Theme.of(context).textTheme.titleMedium,
                    ),
                  ),
                  IconButton(
                      key: const Key('calendar-next'),
                      tooltip: 'Następny miesiąc',
                      icon: const Icon(Icons.chevron_right),
                      onPressed: () => _shift(1)),
                  TextButton(key: const Key('calendar-today'), onPressed: _today, child: const Text('Dzisiaj')),
                  Badge(
                    isLabelVisible: !_filter.isEmpty,
                    label: Text('${_filter.activeCount}'),
                    child: OutlinedButton.icon(
                      key: const Key('calendar-filters'),
                      onPressed: _editFilters,
                      icon: const Icon(Icons.filter_list, size: 18),
                      label: const Text('Filtry'),
                    ),
                  ),
                ],
              ),
            ),
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
              child: Row(
                children: [
                  for (final n in _weekdayNames)
                    Expanded(child: Center(child: Text(n, style: Theme.of(context).textTheme.labelMedium))),
                ],
              ),
            ),
            if (_state == LoadState.loading) const LinearProgressIndicator(minHeight: 2),
            if (_state == LoadState.failed)
              Padding(
                padding: const EdgeInsets.all(12),
                child: Row(
                  children: [
                    Expanded(child: Text(_error ?? 'Nie udało się pobrać postów.')),
                    TextButton(onPressed: _load, child: const Text('Spróbuj ponownie')),
                  ],
                ),
              ),
            Expanded(
              child: Padding(
                padding: const EdgeInsets.fromLTRB(8, 0, 8, 88),
                child: Column(
                  children: [
                    for (var w = 0; w < days ~/ 7; w++)
                      Expanded(
                        child: Row(
                          children: [
                            for (var d = 0; d < 7; d++)
                              Expanded(
                                child: _dayCell(
                                  start.add(Duration(days: w * 7 + d)),
                                  byDay,
                                  today,
                                ),
                              ),
                          ],
                        ),
                      ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _dayCell(DateTime day, Map<DateTime, List<Post>> byDay, DateTime today) {
    final key = DateTime(day.year, day.month, day.day);
    final posts = byDay[key] ?? const <Post>[];
    final inMonth = day.month == _month.month;
    final isToday = key == DateTime(today.year, today.month, today.day);
    final scheme = Theme.of(context).colorScheme;

    return LayoutBuilder(builder: (context, c) {
      const tileHeight = 54.0;
      final fit = ((c.maxHeight - 26) / (tileHeight + 3)).floor().clamp(0, 6);
      final shown = posts.take(posts.length > fit ? (fit > 0 ? fit - 1 : 0) : fit).toList();
      final hidden = posts.length - shown.length;
      return InkWell(
        key: Key('day-${key.year}-${key.month}-${key.day}'),
        onTap: () => _create(key),
        child: Container(
          margin: const EdgeInsets.all(1),
          padding: const EdgeInsets.all(3),
          decoration: BoxDecoration(
            color: inMonth ? null : scheme.surfaceContainerHighest.withOpacity(0.4),
            border: Border.all(color: isToday ? scheme.primary : scheme.outlineVariant, width: isToday ? 2 : 1),
            borderRadius: BorderRadius.circular(6),
          ),
          child: ClipRect(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text('${day.day}',
                    style: Theme.of(context).textTheme.labelSmall?.copyWith(
                        fontWeight: isToday ? FontWeight.bold : null,
                        color: inMonth ? null : scheme.outline)),
                for (final p in shown)
                  Padding(
                    padding: const EdgeInsets.only(top: 3),
                    child: SizedBox(height: tileHeight, child: CalendarPostTile(post: p, onTap: () => _openPost(p))),
                  ),
                if (hidden > 0)
                  Padding(
                    padding: const EdgeInsets.only(top: 3),
                    child: InkWell(
                      key: Key('day-more-${key.year}-${key.month}-${key.day}'),
                      onTap: () => _showDay(key, posts),
                      child: Text('+$hidden więcej',
                          style: Theme.of(context).textTheme.labelSmall?.copyWith(color: scheme.primary)),
                    ),
                  ),
              ],
            ),
          ),
        ),
      );
    });
  }
}

/// Kafelek posta w komórce dnia: tytuł (lewy górny róg), miniatury managerów (inicjały, prawy górny), klient albo firma
/// (lewy dolny róg - firma i manager widzą klienta, klient firmę, która utworzyła post) i ikony platform (prawy dolny).
class CalendarPostTile extends StatelessWidget {
  const CalendarPostTile({super.key, required this.post, required this.onTap});

  final Post post;
  final VoidCallback onTap;

  Color _color(ColorScheme s) => switch (post.status) {
        PostStatus.published => Colors.green.shade100,
        PostStatus.scheduled => s.primaryContainer,
        PostStatus.failed || PostStatus.rejected => s.errorContainer,
        PostStatus.awaitingApproval => s.tertiaryContainer,
        _ => s.surfaceContainerHighest,
      };

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final managers = post.managers.take(3).toList();
    return Material(
      key: Key('tile-${post.id}'),
      color: _color(theme.colorScheme),
      borderRadius: BorderRadius.circular(6),
      child: InkWell(
        borderRadius: BorderRadius.circular(6),
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(4),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Text(post.title,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: theme.textTheme.labelMedium?.copyWith(fontWeight: FontWeight.w600)),
                    Text(post.counterpartName ?? '',
                        maxLines: 1, overflow: TextOverflow.ellipsis, style: theme.textTheme.labelSmall),
                  ],
                ),
              ),
              const SizedBox(width: 4),
              Column(
                crossAxisAlignment: CrossAxisAlignment.end,
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      for (final m in managers)
                        Tooltip(
                          message: m.fullName,
                          child: Padding(
                            padding: const EdgeInsets.only(left: 2),
                            child: CircleAvatar(
                              radius: 8,
                              child: Text(
                                '${m.firstName.isEmpty ? '' : m.firstName[0]}${m.lastName.isEmpty ? '' : m.lastName[0]}',
                                style: const TextStyle(fontSize: 8),
                              ),
                            ),
                          ),
                        ),
                    ],
                  ),
                  PlatformBadges(platforms: post.platforms, size: 14),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}

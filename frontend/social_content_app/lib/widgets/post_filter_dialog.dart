import 'package:flutter/material.dart';

import '../models/enums.dart';
import '../models/manager_user.dart';
import '../models/post_filter.dart';
import '../models/social_account.dart';
import 'platform_icons.dart';

/// Okno filtrów postów (kalendarz i lista). Kategorie łączą się koniunkcją, wartości w kategorii alternatywą.
/// Sekcje włącza się parametrami, bo lista potrzebuje tylko statusu, dat i platform, a kalendarz - statusu,
/// osoby, tagów oraz profili i platform. Zwraca nowy [PostFilter] (Zapisz / Resetuj) albo null (Anuluj).
Future<PostFilter?> showPostFilterDialog(
  BuildContext context, {
  required PostFilter initial,
  List<PostStatus> statuses = PostStatus.values,
  bool showDates = false,
  List<ManagerUser>? managers,
  List<String>? tags,
  List<SocialAccount>? accounts,
  bool showPlatforms = true,
}) =>
    showDialog<PostFilter>(
      context: context,
      builder: (_) => _PostFilterDialog(
        initial: initial,
        statuses: statuses,
        showDates: showDates,
        managers: managers,
        tags: tags,
        accounts: accounts,
        showPlatforms: showPlatforms,
      ),
    );

class _PostFilterDialog extends StatefulWidget {
  const _PostFilterDialog({
    required this.initial,
    required this.statuses,
    required this.showDates,
    required this.managers,
    required this.tags,
    required this.accounts,
    required this.showPlatforms,
  });

  final PostFilter initial;
  final List<PostStatus> statuses;
  final bool showDates;
  final List<ManagerUser>? managers;
  final List<String>? tags;
  final List<SocialAccount>? accounts;
  final bool showPlatforms;

  @override
  State<_PostFilterDialog> createState() => _PostFilterDialogState();
}

class _PostFilterDialogState extends State<_PostFilterDialog> {
  late Set<PostStatus> _statuses = {...widget.initial.statuses};
  late Set<String> _tags = {...widget.initial.tags};
  late Set<String> _accountIds = {...widget.initial.accountIds};
  late Set<SocialPlatform> _platforms = {...widget.initial.platforms};
  late String? _managerId = widget.initial.managerId;
  late DateTime? _from = widget.initial.from;
  late DateTime? _to = widget.initial.to;
  final _newTag = TextEditingController();

  @override
  void dispose() {
    _newTag.dispose();
    super.dispose();
  }

  PostFilter _build() => widget.initial.copyWith(
        statuses: _statuses,
        tags: _tags,
        accountIds: _accountIds,
        platforms: _platforms,
        managerId: _managerId,
        clearManager: _managerId == null,
        from: _from,
        to: _to,
        clearDates: _from == null && _to == null,
      );

  void _reset() => setState(() {
        _statuses = {};
        _tags = {};
        _accountIds = {};
        _platforms = {};
        _managerId = null;
        _from = null;
        _to = null;
      });

  Future<void> _pickDate({required bool from}) async {
    final now = DateTime.now();
    final current = (from ? _from : _to) ?? now;
    final d = await showDatePicker(
      context: context,
      initialDate: current,
      firstDate: DateTime(2000),
      lastDate: DateTime(now.year + 5),
      helpText: from ? 'Od dnia' : 'Do dnia',
    );
    if (d == null) return;
    setState(() {
      if (from) {
        _from = DateTime(d.year, d.month, d.day);
      } else {
        _to = DateTime(d.year, d.month, d.day, 23, 59, 59); // do końca wskazanego dnia
      }
    });
  }

  void _addTag() {
    final t = _newTag.text.trim().replaceFirst(RegExp(r'^#+'), '');
    if (t.isEmpty) return;
    setState(() {
      _tags.add(t);
      _newTag.clear();
    });
  }

  String _date(DateTime? d) =>
      d == null ? 'Wybierz' : '${d.day.toString().padLeft(2, '0')}.${d.month.toString().padLeft(2, '0')}.${d.year}';

  Widget _section(String title, Widget child) => Padding(
        padding: const EdgeInsets.only(top: 16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(title, style: Theme.of(context).textTheme.titleSmall),
            const SizedBox(height: 4),
            child,
          ],
        ),
      );

  @override
  Widget build(BuildContext context) {
    final tagOptions = {...?widget.tags, ..._tags}.toList()..sort((a, b) => a.toLowerCase().compareTo(b.toLowerCase()));
    return AlertDialog(
      title: const Text('Filtry'),
      content: SizedBox(
        width: 440,
        child: SingleChildScrollView(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              _section(
                'Status publikacji',
                Wrap(
                  spacing: 8,
                  children: [
                    for (final s in widget.statuses)
                      FilterChip(
                        key: Key('filter-status-${s.name}'),
                        label: Text(s.label),
                        selected: _statuses.contains(s),
                        onSelected: (v) => setState(() => v ? _statuses.add(s) : _statuses.remove(s)),
                      ),
                  ],
                ),
              ),
              if (widget.showDates)
                _section(
                  'Zakres dat',
                  Row(
                    children: [
                      OutlinedButton(
                          key: const Key('filter-from'),
                          onPressed: () => _pickDate(from: true),
                          child: Text('Od: ${_date(_from)}')),
                      const SizedBox(width: 8),
                      OutlinedButton(
                          key: const Key('filter-to'),
                          onPressed: () => _pickDate(from: false),
                          child: Text('Do: ${_date(_to)}')),
                      if (_from != null || _to != null)
                        IconButton(
                          tooltip: 'Wyczyść daty',
                          icon: const Icon(Icons.close),
                          onPressed: () => setState(() {
                            _from = null;
                            _to = null;
                          }),
                        ),
                    ],
                  ),
                ),
              if (widget.managers != null)
                _section(
                  'Osoba przypisana',
                  DropdownButtonFormField<String?>(
                    key: const Key('filter-manager'),
                    value: widget.managers!.any((m) => m.id == _managerId) ? _managerId : null,
                    isExpanded: true,
                    decoration: const InputDecoration(isDense: true),
                    items: [
                      const DropdownMenuItem<String?>(value: null, child: Text('Wszystkie osoby')),
                      for (final m in widget.managers!)
                        DropdownMenuItem<String?>(value: m.id, child: Text(m.fullName)),
                    ],
                    onChanged: (v) => setState(() => _managerId = v),
                  ),
                ),
              if (widget.tags != null)
                _section(
                  'Tagi treści',
                  Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Wrap(
                        spacing: 8,
                        children: [
                          for (final t in tagOptions)
                            FilterChip(
                              label: Text('#$t'),
                              selected: _tags.any((x) => x.toLowerCase() == t.toLowerCase()),
                              onSelected: (v) => setState(() {
                                _tags.removeWhere((x) => x.toLowerCase() == t.toLowerCase());
                                if (v) _tags.add(t);
                              }),
                            ),
                        ],
                      ),
                      TextField(
                        key: const Key('filter-tag-input'),
                        controller: _newTag,
                        decoration: InputDecoration(
                          isDense: true,
                          hintText: 'Dodaj tag i naciśnij Enter',
                          suffixIcon: IconButton(icon: const Icon(Icons.add), onPressed: _addTag),
                        ),
                        onSubmitted: (_) => _addTag(),
                      ),
                    ],
                  ),
                ),
              if (widget.showPlatforms || widget.accounts != null)
                _section(
                  widget.accounts != null ? 'Profile i platformy' : 'Platformy',
                  Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      if (widget.showPlatforms)
                        Wrap(
                          spacing: 8,
                          children: [
                            for (final p in SocialPlatform.values)
                              FilterChip(
                                key: Key('filter-platform-${p.name}'),
                                avatar: PlatformBadge(platform: p, size: 20),
                                label: Text(p.label),
                                selected: _platforms.contains(p),
                                onSelected: (v) => setState(() => v ? _platforms.add(p) : _platforms.remove(p)),
                              ),
                          ],
                        ),
                      if (widget.accounts != null)
                        for (final a in widget.accounts!)
                          CheckboxListTile(
                            dense: true,
                            contentPadding: EdgeInsets.zero,
                            controlAffinity: ListTileControlAffinity.leading,
                            secondary: PlatformBadge(platform: a.platform, size: 20),
                            title: Text(a.accountName),
                            subtitle: a.ownerName.isEmpty ? null : Text(a.ownerName),
                            value: _accountIds.contains(a.id),
                            onChanged: (v) => setState(() => v == true ? _accountIds.add(a.id) : _accountIds.remove(a.id)),
                          ),
                    ],
                  ),
                ),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(key: const Key('filter-reset'), onPressed: _reset, child: const Text('Resetuj')),
        TextButton(onPressed: () => Navigator.pop(context), child: const Text('Anuluj')),
        FilledButton(
            key: const Key('filter-save'),
            onPressed: () => Navigator.pop(context, _build()),
            child: const Text('Zapisz')),
      ],
    );
  }
}

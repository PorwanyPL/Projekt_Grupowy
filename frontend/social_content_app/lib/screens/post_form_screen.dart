import 'dart:async';

import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/enums.dart';
import '../models/post.dart';
import '../models/manager_user.dart';
import '../models/social_account.dart';
import '../providers/post_provider.dart';
import '../providers/social_account_provider.dart';
import '../services/api_exception.dart';
import '../services/media_service.dart';
import '../utils/formatters.dart';
import '../utils/validators.dart';
import '../widgets/account_menu.dart';
import '../widgets/app_messenger.dart';
import '../widgets/platform_icons.dart';
import '../widgets/platform_preview.dart';

enum PublishMode { now, schedule, draft }

/// Formularz posta. Ten sam dla CompanyUser (profile wszystkich klientów firmy) i ManagerUser (profile, do których jest
/// przypisany); tworzenie i edycja wyglądają tak samo. Najpierw wybiera się klienta, potem platformy (ikony profili tego
/// klienta) - wiele platform to jeden logiczny post. Data publikacji pochodzi z klikniętego dnia kalendarza
/// ([initialDate]) i można ją zmienić. Współautorów ustawia serwer (managerowie przypisani do kont).
/// Przy edycji formularz zajmuje blokadę edycji i odnawia ją co minutę; zwalnia ją przy wyjściu.
/// Edycja zaplanowanego lub opublikowanego posta nie cofa go do akceptacji klienta (serwer pomija ją przy edycji).
/// Opublikowany post też można edytować (zapis zmienia treść w serwisie, bez ponownej publikacji i planowania).
/// Gdy zajęcie blokady się nie uda (ktoś inny edytuje), formularz się zamyka i pokazuje jeden komunikat.
class PostFormScreen extends StatefulWidget {
  const PostFormScreen({super.key, this.post, this.initialDate, this.initialAccountIds = const []});

  /// null = tworzenie, w przeciwnym razie edycja.
  final Post? post;

  /// Dzień kliknięty w kalendarzu - domyślny termin publikacji (godzina 12:00, edytowalna).
  final DateTime? initialDate;

  /// Wstępnie zaznaczone konta (np. ekran szczegółów konta).
  final List<String> initialAccountIds;

  @override
  State<PostFormScreen> createState() => _PostFormScreenState();
}

class _PostFormScreenState extends State<PostFormScreen> {
  static const _imageExtensions = ['png', 'jpg', 'jpeg', 'gif', 'webp'];
  static const _videoExtensions = ['mp4', 'mp3', 'flac'];
  static const _maxImageBytes = 10 * 1024 * 1024;
  static const _maxVideoBytes = 50 * 1024 * 1024;

  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _title;
  late final TextEditingController _description;
  late final PostProvider _posts;
  late final MediaService _media;

  PostType _type = PostType.text;
  final List<MediaInput> _pictures = [];
  final List<MediaInput> _videos = [];

  PublishMode _mode = PublishMode.now;
  DateTime? _scheduledAt;

  // Klient i wybrane konta (platformy); kilka kont = jeden logiczny post.
  late final List<SocialAccount> _accounts;
  String? _clientId;
  final Set<String> _selected = {};
  String? _previewId;
  String? _accountError;

  final List<String> _tags = [];
  final _tagInput = TextEditingController();

  bool _saving = false;
  bool _uploading = false;
  String? _error;
  String? _mediaError;
  String? _scheduleError;

  // Blokada edycji (tylko przy edycji istniejącego posta).
  bool _lockChecked = false;
  String? _lockedMessage;
  String? _lockLostMessage;
  Timer? _heartbeat;
  bool _lockHeld = false;

  /// Termin publikacji dla klikniętego dnia: 12:00; dla dzisiaj - najwcześniej za 10 minut.
  static DateTime _defaultTimeOn(DateTime day) {
    final noon = DateTime(day.year, day.month, day.day, 12);
    final soon = DateTime.now().add(const Duration(minutes: 10));
    return noon.isAfter(soon) ? noon : soon;
  }

  /// Klienci (właściciele profili) dostępni dla zalogowanej roli.
  List<({String id, String name})> get _clientOptions {
    final seen = <String>{};
    return [
      for (final a in _accounts)
        if (seen.add(a.ownerClientId))
          (id: a.ownerClientId, name: a.ownerName.isEmpty ? 'Klient' : a.ownerName),
    ];
  }

  List<SocialAccount> get _clientAccounts =>
      _accounts.where((a) => a.ownerClientId == _clientId).toList();

  List<SocialAccount> get _selectedAccounts =>
      [for (final a in _accounts) if (_selected.contains(a.id)) a];

  bool get _isEdit => widget.post != null;
  bool get _isPublished => widget.post?.status == PostStatus.published;

  @override
  void initState() {
    super.initState();
    _posts = context.read<PostProvider>();
    _media = context.read<MediaService>();
    final p = widget.post;
    _accounts = context
        .read<SocialAccountProvider>()
        .items
        .where((a) => a.isAccepted || (p?.accountIds.contains(a.id) ?? false))
        .toList();
    _title = TextEditingController(text: p?.title ?? '')..addListener(_refresh);
    _description = TextEditingController(text: p?.description ?? '')..addListener(_refresh);
    if (p != null) {
      _type = p.visibility;
      _pictures.addAll(p.pictures.map((x) => MediaInput(
          storageKey: x.storageKey, fileName: x.fileName, format: x.format, sizeKb: x.sizeKb)));
      _videos.addAll(p.videos.map((x) => MediaInput(
          storageKey: x.storageKey, fileName: x.fileName, format: x.format, sizeKb: x.sizeKb)));
      _tags.addAll(p.tags);
      _selected.addAll(p.accountIds);
      if (p.scheduledAt != null) {
        _mode = PublishMode.schedule;
        _scheduledAt = p.scheduledAt;
      } else if (p.status == PostStatus.draft) {
        _mode = PublishMode.draft;
      }
      _acquireLock(p.id);
    } else {
      _selected.addAll(widget.initialAccountIds);
      final day = widget.initialDate;
      if (day != null) {
        _mode = PublishMode.schedule;
        _scheduledAt = _defaultTimeOn(day);
      }
      _lockChecked = true;
    }
    final first = _selected.firstOrNull;
    _clientId = _accounts.where((a) => a.id == first).firstOrNull?.ownerClientId ??
        (_clientOptions.length == 1 ? _clientOptions.first.id : null);
    _previewId = first;
  }

  @override
  void dispose() {
    _heartbeat?.cancel();
    if (_lockHeld && widget.post != null) _posts.releaseLock(widget.post!.id);
    _title.dispose();
    _description.dispose();
    _tagInput.dispose();
    super.dispose();
  }

  void _refresh() => setState(() {});

  Future<void> _acquireLock(String id) async {
    try {
      await _posts.acquireLock(id);
      _lockHeld = true;
      _heartbeat = Timer.periodic(const Duration(minutes: 1), (_) => _renewLock(id));
    } on ApiException catch (e) {
      _lockedMessage = e.message;
    }
    if (!mounted) return;
    if (_lockedMessage != null) {
      // Zamiast osobnego ekranu z błędem: wróć do listy i pokaż jeden komunikat (duplikaty scala AppMessenger).
      context.read<AppMessenger>().show(_lockedMessage!);
      Navigator.of(context).pop();
      return;
    }
    setState(() => _lockChecked = true);
  }

  Future<void> _renewLock(String id) async {
    try {
      await _posts.acquireLock(id);
      if (mounted && _lockLostMessage != null) setState(() => _lockLostMessage = null);
    } on ApiException catch (e) {
      if (mounted) setState(() => _lockLostMessage = e.message);
    }
  }

  void _changeType(PostType type) {
    setState(() {
      _type = type;
      _mediaError = null;
      if (type == PostType.text) {
        _pictures.clear();
        _videos.clear();
      } else if (type == PostType.photo) {
        _videos.clear();
      }
    });
  }

  Future<void> _addMedia({required bool isVideo}) async {
    final result = await FilePicker.platform.pickFiles(
      type: FileType.custom,
      allowedExtensions: isVideo ? _videoExtensions : _imageExtensions,
      withData: true,
    );
    final file = result?.files.firstOrNull;
    if (file == null) return;
    final bytes = file.bytes;
    if (bytes == null) {
      setState(() => _mediaError = 'Nie udało się odczytać pliku.');
      return;
    }
    // Szybka kontrola po stronie klienta; ostateczna (typ, rozmiar, zawartość) jest na serwerze.
    final limit = isVideo ? _maxVideoBytes : _maxImageBytes;
    if (bytes.length > limit) {
      setState(() => _mediaError =
          'Plik jest za duży (maks. ${limit ~/ (1024 * 1024)} MB dla ${isVideo ? 'wideo' : 'zdjęć'}).');
      return;
    }
    setState(() {
      _uploading = true;
      _mediaError = null;
    });
    try {
      final uploaded = await _media.upload(bytes, file.name);
      if (!mounted) return;
      String? path;
      try {
        path = file.path; // na Windows/desktop: plik z dysku do natychmiastowego podglądu wideo
      } catch (_) {
        path = null; // web - brak ścieżki
      }
      setState(() => (isVideo ? _videos : _pictures).add(uploaded.withLocalPath(path)));
    } on ApiException catch (e) {
      if (mounted) setState(() => _mediaError = e.message);
    } finally {
      if (mounted) setState(() => _uploading = false);
    }
  }

  String? _validateMedia() {
    switch (_type) {
      case PostType.text:
        return null;
      case PostType.photo:
        return _pictures.isEmpty ? 'Post typu Zdjęcie wymaga co najmniej jednego zdjęcia.' : null;
      case PostType.video:
        return _videos.isEmpty ? 'Post typu Wideo wymaga co najmniej jednego wideo.' : null;
    }
  }

  String? _validateAccounts() =>
      _selected.isEmpty ? 'Wybierz co najmniej jedną platformę (profil klienta).' : null;

  void _addTag([String? value]) {
    final t = (value ?? _tagInput.text).trim().replaceFirst(RegExp(r'^#+'), '');
    if (t.isEmpty) return;
    if (t.length > 30) {
      setState(() => _error = 'Tag może mieć maksymalnie 30 znaków.');
      return;
    }
    if (_tags.length >= 10) {
      setState(() => _error = 'Post może mieć maksymalnie 10 tagów.');
      return;
    }
    setState(() {
      _error = null;
      if (!_tags.any((x) => x.toLowerCase() == t.toLowerCase())) _tags.add(t);
      _tagInput.clear();
    });
  }

  /// Planowany termin musi być w przyszłości (serwer sprawdza to ponownie).
  String? _validateSchedule() {
    if (_mode != PublishMode.schedule) return null;
    if (_scheduledAt == null) return 'Wybierz dzień i godzinę publikacji.';
    if (!_scheduledAt!.isAfter(DateTime.now())) {
      return 'Termin publikacji musi być w przyszłości.';
    }
    return null;
  }

  Future<void> _pickSchedule() async {
    final now = DateTime.now();
    final initial = (_scheduledAt != null && _scheduledAt!.isAfter(now))
        ? _scheduledAt!
        : now.add(const Duration(hours: 1));
    final date = await showDatePicker(
      context: context,
      initialDate: initial,
      firstDate: DateTime(now.year, now.month, now.day),
      lastDate: now.add(const Duration(days: 365 * 2)),
      helpText: 'Dzień publikacji',
    );
    if (date == null || !mounted) return;
    final time = await showTimePicker(
      context: context,
      initialTime: TimeOfDay.fromDateTime(initial),
      helpText: 'Godzina publikacji',
    );
    if (time == null || !mounted) return;
    setState(() {
      _scheduledAt = DateTime(date.year, date.month, date.day, time.hour, time.minute);
      _scheduleError = _validateSchedule();
    });
  }

  Future<void> _submit() async {
    final formOk = _formKey.currentState!.validate();
    final mediaError = _validateMedia();
    final scheduleError = _isPublished ? null : _validateSchedule();
    final accountError = _validateAccounts();
    setState(() {
      _mediaError = mediaError;
      _scheduleError = scheduleError;
      _accountError = accountError;
    });
    if (!formOk || mediaError != null || scheduleError != null || accountError != null) return;

    setState(() {
      _saving = true;
      _error = null;
    });

    final input = PostInput(
      title: _title.text.trim(),
      description: _description.text.trim(),
      type: _type,
      pictures: List.of(_pictures),
      videos: List.of(_videos),
      targetAccountIds: _selected.toList(),
      tags: List.of(_tags),
      // Opublikowany post: tylko zapis zmian - bez ponownej publikacji i planowania.
      publishNow: !_isPublished && _mode == PublishMode.now,
      scheduledAt: !_isPublished && _mode == PublishMode.schedule ? _scheduledAt : null,
    );
    final messenger = context.read<AppMessenger>();

    try {
      final saved = _isEdit
          ? await _posts.update(widget.post!.id, input)
          : await _posts.create(input);
      _lockHeld = false; // serwer zwolnił blokadę przy zapisie
      if (saved.status == PostStatus.failed) {
        messenger.show('Publikacja nie powiodła się: ${saved.errorMessage ?? 'nieznany błąd'}');
      } else if (saved.status == PostStatus.awaitingApproval) {
        messenger.show('Post czeka na akceptację klienta.');
      } else if (saved.status == PostStatus.published) {
        messenger.show(_isPublished ? 'Zmiany zostały zapisane.' : 'Post został opublikowany.');
      } else if (saved.status == PostStatus.scheduled) {
        messenger.show(_isEdit ? 'Zmiany zostały zapisane.' : 'Post został zaplanowany.');
      } else if (saved.status == PostStatus.draft) {
        messenger.show('Szkic został zapisany.');
      }
      if (mounted) Navigator.pop(context);
    } on ApiException catch (e) {
      if (mounted) {
        setState(() {
          _saving = false;
          _error = e.message;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    final appBar = AppBar(
      title: Text(_isEdit ? 'Edytuj post' : 'Nowy post'),
      actions: accountMenuActions(context),
    );

    final previewAccount =
        _selectedAccounts.where((a) => a.id == _previewId).firstOrNull ?? _selectedAccounts.firstOrNull;

    if (!_lockChecked) {
      return Scaffold(appBar: appBar, body: const Center(child: CircularProgressIndicator()));
    }
    return Scaffold(
      appBar: appBar,
      body: Form(
        key: _formKey,
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            if (_lockLostMessage != null) ...[
              Card(
                color: theme.colorScheme.errorContainer,
                child: Padding(
                  padding: const EdgeInsets.all(12),
                  child: Text(_lockLostMessage!),
                ),
              ),
              const SizedBox(height: 16),
            ],
            _accountsSection(theme),
            const SizedBox(height: 16),
            TextFormField(
              controller: _title,
              decoration: const InputDecoration(labelText: 'Tytuł'),
              textInputAction: TextInputAction.next,
              validator: (v) => FormValidators.text(v, 'Tytuł', 120),
            ),
            const SizedBox(height: 16),
            TextFormField(
              controller: _description,
              decoration: const InputDecoration(labelText: 'Opis', alignLabelWithHint: true),
              maxLines: 5,
              validator: (v) => FormValidators.text(v, 'Opis', 2000),
            ),
            const SizedBox(height: 16),
            Text('Typ posta', style: theme.textTheme.titleSmall),
            const SizedBox(height: 8),
            SegmentedButton<PostType>(
              segments: [
                for (final t in PostType.values) ButtonSegment(value: t, label: Text(t.label)),
              ],
              selected: {_type},
              onSelectionChanged: (s) => _changeType(s.first),
            ),
            if (_type != PostType.text) ...[
              const SizedBox(height: 16),
              _mediaSection(title: 'Zdjęcia', items: _pictures, isVideo: false),
            ],
            if (_type == PostType.video) ...[
              const SizedBox(height: 8),
              _mediaSection(title: 'Wideo', items: _videos, isVideo: true),
            ],
            if (_mediaError != null) ...[
              const SizedBox(height: 8),
              Text(_mediaError!, style: TextStyle(color: theme.colorScheme.error)),
            ],
            const SizedBox(height: 16),
            if (previewAccount == null)
              const Text('Podgląd pojawi się po wybraniu platformy.')
            else ...[
              Text('Podgląd: ${previewAccount.platform.label}', style: theme.textTheme.titleSmall),
              if (_selectedAccounts.length > 1) ...[
                const SizedBox(height: 4),
                Wrap(
                  spacing: 8,
                  children: [
                    for (final a in _selectedAccounts)
                      ChoiceChip(
                        key: Key('preview-chip-${a.id}'),
                        avatar: PlatformBadge(platform: a.platform, size: 18),
                        label: Text(a.platform.label),
                        selected: a.id == previewAccount.id,
                        onSelected: (_) => setState(() => _previewId = a.id),
                      ),
                  ],
                ),
              ],
              const SizedBox(height: 8),
              PlatformPreview(
                platform: previewAccount.platform,
                accountName: previewAccount.accountName,
                title: _title.text,
                description: _description.text,
                pictures: _pictures,
                videos: _videos,
                imageUrl: _media.urlFor,
                imageHeaders: _media.headers,
                videoUrl: _media.playbackUrlFor,
                videoHeaders: _media.headers,
                lazyVideo: true,
              ),
            ],
            const SizedBox(height: 16),
            if (_isPublished)
              const Text('Post jest już opublikowany - zapis zaktualizuje jego treść.',
                  key: Key('published-info'))
            else ...[
            Text('Publikacja', style: theme.textTheme.titleSmall),
            if (!_isEdit && _selectedAccounts.any((a) => a.approvalMode == PostApprovalMode.client && a.isAccepted))
              const Padding(
                padding: EdgeInsets.only(bottom: 4),
                child: Text('Klient zatwierdza posty przed publikacją - post trafi najpierw do jego akceptacji.',
                    key: Key('approval-info')),
              ),
            RadioListTile<PublishMode>(
              contentPadding: EdgeInsets.zero,
              title: const Text('Publikuj teraz'),
              value: PublishMode.now,
              groupValue: _mode,
              onChanged: (v) => setState(() {
                _mode = v!;
                _scheduleError = null;
              }),
            ),
            RadioListTile<PublishMode>(
              contentPadding: EdgeInsets.zero,
              title: const Text('Zaplanuj'),
              value: PublishMode.schedule,
              groupValue: _mode,
              onChanged: (v) => setState(() => _mode = v!),
            ),
            RadioListTile<PublishMode>(
              key: const Key('mode-draft'),
              contentPadding: EdgeInsets.zero,
              title: const Text('Zapisz jako szkic'),
              value: PublishMode.draft,
              groupValue: _mode,
              onChanged: (v) => setState(() {
                _mode = v!;
                _scheduleError = null;
              }),
            ),
            if (_mode == PublishMode.schedule) ...[
              OutlinedButton.icon(
                onPressed: _pickSchedule,
                icon: const Icon(Icons.event),
                label: Text(_scheduledAt == null
                    ? 'Wybierz dzień i godzinę'
                    : formatDateTime(_scheduledAt!)),
              ),
              if (_scheduleError != null)
                Padding(
                  padding: const EdgeInsets.only(top: 8),
                  child: Text(_scheduleError!, style: TextStyle(color: theme.colorScheme.error)),
                ),
            ],
            ],
            const SizedBox(height: 16),
            _tagsSection(theme),
            const SizedBox(height: 16),
            Text('Współautorzy (dodawani automatycznie)', style: theme.textTheme.titleSmall),
            const SizedBox(height: 8),
            if (_coauthors.isEmpty)
              const Text('Do wybranych kont nie jest przypisany żaden manager.')
            else
              Wrap(
                spacing: 8,
                runSpacing: 4,
                children: [
                  for (final m in _coauthors)
                    Chip(
                      avatar: const Icon(Icons.person, size: 16),
                      label: Text(m.fullName),
                      visualDensity: VisualDensity.compact,
                    ),
                ],
              ),
            if (_error != null) ...[
              const SizedBox(height: 16),
              Text(_error!, style: TextStyle(color: theme.colorScheme.error)),
            ],
            const SizedBox(height: 24),
            FilledButton(
              onPressed: (_saving || _uploading) ? null : _submit,
              child: _saving
                  ? const SizedBox(
                      height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
                  : Text(_isPublished
                      ? 'Zapisz zmiany'
                      : _mode == PublishMode.draft
                          ? 'Zapisz szkic'
                          : _mode == PublishMode.now
                              ? (_isEdit ? 'Zapisz i opublikuj' : 'Opublikuj post')
                              : (_isEdit ? 'Zapisz i zaplanuj' : 'Zaplanuj post')),
            ),
          ],
        ),
      ),
    );
  }

  /// Unikalni managerowie wybranych kont (serwer dodaje ich jako współautorów).
  List<ManagerUser> get _coauthors {
    final seen = <String>{};
    return [
      for (final a in _selectedAccounts)
        for (final m in a.managers)
          if (seen.add(m.id)) m,
    ];
  }

  Widget _accountsSection(ThemeData theme) {
    final clients = _clientOptions;
    return Card(
      key: const Key('accounts-section'),
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Klient i platformy', style: theme.textTheme.titleSmall),
            const SizedBox(height: 8),
            if (clients.isEmpty)
              const Text('Brak profili do obsługi - przyjmij zaproszenie klienta w zakładce Konta.')
            else ...[
              if (clients.length > 1)
                DropdownButtonFormField<String>(
                  key: const Key('client-select'),
                  value: clients.any((c) => c.id == _clientId) ? _clientId : null,
                  isExpanded: true,
                  decoration: const InputDecoration(labelText: 'Klient', isDense: true),
                  items: [for (final c in clients) DropdownMenuItem(value: c.id, child: Text(c.name))],
                  onChanged: _isEdit
                      ? null
                      : (v) => setState(() {
                            _clientId = v;
                            _selected.clear(); // wybór platform dotyczy jednego klienta
                            _previewId = null;
                          }),
                )
              else
                Text('Klient: ${clients.first.name}'),
              const SizedBox(height: 8),
              if (_clientId == null)
                const Text('Wybierz klienta, aby zobaczyć jego platformy.')
              else
                Wrap(
                  spacing: 8,
                  runSpacing: 4,
                  children: [
                    for (final a in _clientAccounts)
                      FilterChip(
                        key: Key('account-chip-${a.id}'),
                        avatar: PlatformBadge(platform: a.platform, size: 22),
                        label: Text('${a.platform.label} · ${a.accountName}'),
                        selected: _selected.contains(a.id),
                        onSelected: (v) => setState(() {
                          v ? _selected.add(a.id) : _selected.remove(a.id);
                          _accountError = null;
                          if (_previewId == null || !_selected.contains(_previewId)) {
                            _previewId = _selected.firstOrNull;
                          }
                        }),
                      ),
                  ],
                ),
            ],
            if (_accountError != null)
              Padding(
                padding: const EdgeInsets.only(top: 8),
                child: Text(_accountError!, style: TextStyle(color: theme.colorScheme.error)),
              ),
          ],
        ),
      ),
    );
  }

  Widget _tagsSection(ThemeData theme) {
    final suggestions = context
        .read<PostProvider>()
        .knownTags
        .where((t) => !_tags.any((x) => x.toLowerCase() == t.toLowerCase()))
        .take(8)
        .toList();
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('Tagi', style: theme.textTheme.titleSmall),
        const SizedBox(height: 8),
        TextField(
          key: const Key('tag-input'),
          controller: _tagInput,
          maxLength: 30,
          decoration: InputDecoration(
            hintText: 'Dodaj tag i naciśnij Enter',
            counterText: '',
            suffixIcon: IconButton(
              tooltip: 'Dodaj tag',
              icon: const Icon(Icons.add),
              onPressed: _addTag,
            ),
          ),
          onSubmitted: _addTag,
        ),
        Wrap(
          spacing: 8,
          children: [
            for (final t in _tags)
              InputChip(label: Text('#$t'), onDeleted: () => setState(() => _tags.remove(t))),
            for (final t in suggestions)
              ActionChip(label: Text('+ #$t'), onPressed: () => _addTag(t)),
          ],
        ),
      ],
    );
  }

  Widget _mediaSection({
    required String title,
    required List<MediaInput> items,
    required bool isVideo,
  }) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Text(title, style: Theme.of(context).textTheme.titleSmall),
                const Spacer(),
                if (_uploading)
                  const SizedBox(
                      height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2))
                else
                  TextButton.icon(
                    onPressed: () => _addMedia(isVideo: isVideo),
                    icon: const Icon(Icons.upload_file),
                    label: const Text('Wybierz plik'),
                  ),
              ],
            ),
            if (items.isEmpty) const Text('Brak plików'),
            for (var i = 0; i < items.length; i++)
              ListTile(
                dense: true,
                contentPadding: EdgeInsets.zero,
                leading: Icon(isVideo ? Icons.videocam : Icons.image),
                title: Text(items[i].fileName),
                subtitle: Text(items[i].format.label),
                trailing: IconButton(
                  tooltip: 'Usuń plik',
                  icon: const Icon(Icons.close),
                  onPressed: () => setState(() => items.removeAt(i)),
                ),
              ),
          ],
        ),
      ),
    );
  }
}

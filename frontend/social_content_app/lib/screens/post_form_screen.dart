import 'dart:async';

import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/enums.dart';
import '../models/post.dart';
import '../models/social_account.dart';
import '../providers/post_provider.dart';
import '../services/api_exception.dart';
import '../services/media_service.dart';
import '../utils/formatters.dart';
import '../utils/validators.dart';
import '../widgets/platform_preview.dart';

enum PublishMode { now, schedule }

/// Formularz posta. Ten sam dla CompanyUser (dowolne konto firmy, np. z widoku admina) i ManagerUser
/// (konto, do którego jest przypisany). Współautorów ustawia serwer (managerowie przypisani do konta).
/// Przy edycji formularz zajmuje blokadę edycji i odnawia ją co minutę; zwalnia ją przy wyjściu.
class PostFormScreen extends StatefulWidget {
  const PostFormScreen({super.key, required this.account, this.post});

  /// Konto, na którym powstaje post (decyduje o podglądzie platformy).
  final SocialAccount account;

  /// null = tworzenie, w przeciwnym razie edycja.
  final Post? post;

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

  bool get _isEdit => widget.post != null;

  @override
  void initState() {
    super.initState();
    _posts = context.read<PostProvider>();
    _media = context.read<MediaService>();
    final p = widget.post;
    _title = TextEditingController(text: p?.title ?? '')..addListener(_refresh);
    _description = TextEditingController(text: p?.description ?? '')..addListener(_refresh);
    if (p != null) {
      _type = p.visibility;
      _pictures.addAll(p.pictures.map((x) => MediaInput(
          storageKey: x.storageKey, fileName: x.fileName, format: x.format, sizeKb: x.sizeKb)));
      _videos.addAll(p.videos.map((x) => MediaInput(
          storageKey: x.storageKey, fileName: x.fileName, format: x.format, sizeKb: x.sizeKb)));
      if (p.scheduledAt != null) {
        _mode = PublishMode.schedule;
        _scheduledAt = p.scheduledAt;
      }
      _acquireLock(p.id);
    } else {
      _lockChecked = true;
    }
  }

  @override
  void dispose() {
    _heartbeat?.cancel();
    if (_lockHeld && widget.post != null) _posts.releaseLock(widget.post!.id);
    _title.dispose();
    _description.dispose();
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
    if (mounted) setState(() => _lockChecked = true);
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
      setState(() => (isVideo ? _videos : _pictures).add(uploaded));
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
    final scheduleError = _validateSchedule();
    setState(() {
      _mediaError = mediaError;
      _scheduleError = scheduleError;
    });
    if (!formOk || mediaError != null || scheduleError != null) return;

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
      targetAccountId: widget.account.id,
      publishNow: _mode == PublishMode.now,
      scheduledAt: _mode == PublishMode.schedule ? _scheduledAt : null,
    );
    final messenger = ScaffoldMessenger.of(context);

    try {
      final saved = _isEdit
          ? await _posts.update(widget.post!.id, input)
          : await _posts.create(input);
      _lockHeld = false; // serwer zwolnił blokadę przy zapisie
      if (saved.status == PostStatus.failed) {
        messenger.showSnackBar(SnackBar(
          content: Text('Publikacja nie powiodła się: ${saved.errorMessage ?? 'nieznany błąd'}'),
        ));
      } else if (saved.status == PostStatus.published) {
        messenger.showSnackBar(const SnackBar(content: Text('Post został opublikowany.')));
      } else if (saved.status == PostStatus.scheduled) {
        messenger.showSnackBar(const SnackBar(content: Text('Post został zaplanowany.')));
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
      bottom: PreferredSize(
        preferredSize: const Size.fromHeight(28),
        child: Padding(
          padding: const EdgeInsets.only(bottom: 8),
          child: Text('Konto: ${widget.account.accountName} (${widget.account.platform.label})'),
        ),
      ),
    );

    if (!_lockChecked) {
      return Scaffold(appBar: appBar, body: const Center(child: CircularProgressIndicator()));
    }
    if (_lockedMessage != null) {
      return Scaffold(
        appBar: appBar,
        body: Center(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Icon(Icons.lock_outline, size: 48),
                const SizedBox(height: 16),
                Text(_lockedMessage!, textAlign: TextAlign.center),
                const SizedBox(height: 24),
                FilledButton(
                  onPressed: () => Navigator.pop(context),
                  child: const Text('Wróć'),
                ),
              ],
            ),
          ),
        ),
      );
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
            Text('Podgląd: ${widget.account.platform.label}', style: theme.textTheme.titleSmall),
            const SizedBox(height: 8),
            PlatformPreview(
              platform: widget.account.platform,
              accountName: widget.account.accountName,
              title: _title.text,
              description: _description.text,
              pictures: _pictures,
              videos: _videos,
              imageUrl: _media.urlFor,
              imageHeaders: _media.headers,
            ),
            const SizedBox(height: 16),
            Text('Publikacja', style: theme.textTheme.titleSmall),
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
            const SizedBox(height: 16),
            Text('Współautorzy (dodawani automatycznie)', style: theme.textTheme.titleSmall),
            const SizedBox(height: 8),
            if (widget.account.managers.isEmpty)
              const Text('Do tego konta nie jest przypisany żaden manager.')
            else
              Wrap(
                spacing: 8,
                runSpacing: 4,
                children: [
                  for (final m in widget.account.managers)
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
                  : Text(_mode == PublishMode.now
                      ? (_isEdit ? 'Zapisz i opublikuj' : 'Opublikuj post')
                      : (_isEdit ? 'Zapisz i zaplanuj' : 'Zaplanuj post')),
            ),
          ],
        ),
      ),
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

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/enums.dart';
import '../models/post.dart';
import '../providers/manager_provider.dart';
import '../providers/post_provider.dart';
import '../services/api_exception.dart';
import '../utils/formatters.dart';
import '../utils/validators.dart';
import '../widgets/media_dialog.dart';

class PostFormScreen extends StatefulWidget {
  const PostFormScreen({super.key, this.post});

  /// null = tworzenie, w przeciwnym razie edycja.
  final Post? post;

  @override
  State<PostFormScreen> createState() => _PostFormScreenState();
}

class _PostFormScreenState extends State<PostFormScreen> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _title;
  late final TextEditingController _description;

  PostType _type = PostType.text;
  final List<MediaInput> _pictures = [];
  final List<MediaInput> _videos = [];
  final Set<String> _managerIds = {};

  bool _saving = false;
  String? _error;
  String? _mediaError;

  bool get _isEdit => widget.post != null;

  @override
  void initState() {
    super.initState();
    final p = widget.post;
    _title = TextEditingController(text: p?.title ?? '');
    _description = TextEditingController(text: p?.description ?? '');
    if (p != null) {
      _type = p.visibility;
      _pictures.addAll(p.pictures.map((x) =>
          MediaInput(fileName: x.fileName, format: x.format, amount: x.sizeKb)));
      _videos.addAll(p.videos.map((x) =>
          MediaInput(fileName: x.fileName, format: x.format, amount: x.length)));
      _managerIds.addAll(p.managers.map((m) => m.id));
    }
  }

  @override
  void dispose() {
    _title.dispose();
    _description.dispose();
    super.dispose();
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
    final item = await showMediaDialog(context, isVideo: isVideo);
    if (item == null) return;
    setState(() {
      (isVideo ? _videos : _pictures).add(item);
      _mediaError = null;
    });
  }

  String? _validateMedia() {
    switch (_type) {
      case PostType.text:
        return null;
      case PostType.photo:
        return _pictures.isEmpty
            ? 'Post typu Zdjęcie wymaga co najmniej jednego zdjęcia.'
            : null;
      case PostType.video:
        return _videos.isEmpty
            ? 'Post typu Wideo wymaga co najmniej jednego wideo.'
            : null;
    }
  }

  Future<void> _submit() async {
    final formOk = _formKey.currentState!.validate();
    final mediaError = _validateMedia();
    setState(() => _mediaError = mediaError);
    if (!formOk || mediaError != null) return;

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
      managerIds: _managerIds.toList(),
    );
    final provider = context.read<PostProvider>();

    try {
      if (_isEdit) {
        await provider.update(widget.post!.id, input);
      } else {
        await provider.create(input);
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
    final managers = context.watch<ManagerProvider>().items;
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(title: Text(_isEdit ? 'Edytuj post' : 'Nowy post')),
      body: Form(
        key: _formKey,
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            TextFormField(
              controller: _title,
              decoration: const InputDecoration(labelText: 'Tytuł'),
              textInputAction: TextInputAction.next,
              validator: (v) => FormValidators.text(v, 'Tytuł', 120),
            ),
            const SizedBox(height: 16),
            TextFormField(
              controller: _description,
              decoration: const InputDecoration(
                labelText: 'Opis',
                alignLabelWithHint: true,
              ),
              maxLines: 5,
              validator: (v) => FormValidators.text(v, 'Opis', 2000),
            ),
            const SizedBox(height: 16),
            Text('Typ posta', style: theme.textTheme.titleSmall),
            const SizedBox(height: 8),
            SegmentedButton<PostType>(
              segments: [
                for (final t in PostType.values)
                  ButtonSegment(value: t, label: Text(t.label)),
              ],
              selected: {_type},
              onSelectionChanged: (s) => _changeType(s.first),
            ),
            if (_type != PostType.text) ...[
              const SizedBox(height: 16),
              _mediaSection(
                title: 'Zdjęcia',
                items: _pictures,
                isVideo: false,
              ),
            ],
            if (_type == PostType.video) ...[
              const SizedBox(height: 8),
              _mediaSection(
                title: 'Wideo',
                items: _videos,
                isVideo: true,
              ),
            ],
            if (_mediaError != null) ...[
              const SizedBox(height: 8),
              Text(
                _mediaError!,
                style: TextStyle(color: theme.colorScheme.error),
              ),
            ],
            const SizedBox(height: 16),
            Text('Przypisani managerowie', style: theme.textTheme.titleSmall),
            const SizedBox(height: 8),
            if (managers.isEmpty)
              const Text('Brak managerów - dodaj ich w zakładce "Managerowie".')
            else
              Wrap(
                spacing: 8,
                runSpacing: 4,
                children: [
                  for (final m in managers)
                    FilterChip(
                      label: Text(m.fullName),
                      selected: _managerIds.contains(m.id),
                      onSelected: (on) => setState(() {
                        if (on) {
                          _managerIds.add(m.id);
                        } else {
                          _managerIds.remove(m.id);
                        }
                      }),
                    ),
                ],
              ),
            if (_error != null) ...[
              const SizedBox(height: 16),
              Text(_error!, style: TextStyle(color: theme.colorScheme.error)),
            ],
            const SizedBox(height: 24),
            FilledButton(
              onPressed: _saving ? null : _submit,
              child: _saving
                  ? const SizedBox(
                      height: 20,
                      width: 20,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : Text(_isEdit ? 'Zapisz zmiany' : 'Utwórz post'),
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
                TextButton.icon(
                  onPressed: () => _addMedia(isVideo: isVideo),
                  icon: const Icon(Icons.add),
                  label: const Text('Dodaj'),
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
                subtitle: Text(
                  '${items[i].format.label} · '
                  '${isVideo ? formatDuration(items[i].amount) : '${items[i].amount} KB'}',
                ),
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

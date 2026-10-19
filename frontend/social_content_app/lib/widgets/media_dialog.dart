import 'package:flutter/material.dart';

import '../models/enums.dart';
import '../models/post.dart';
import '../utils/validators.dart';

/// Dialog dodawania pliku (zdjęcia lub wideo). Zwraca null po anulowaniu.
Future<MediaInput?> showMediaDialog(
  BuildContext context, {
  required bool isVideo,
}) {
  return showDialog<MediaInput>(
    context: context,
    builder: (_) => _MediaDialog(isVideo: isVideo),
  );
}

class _MediaDialog extends StatefulWidget {
  const _MediaDialog({required this.isVideo});

  final bool isVideo;

  @override
  State<_MediaDialog> createState() => _MediaDialogState();
}

class _MediaDialogState extends State<_MediaDialog> {
  final _formKey = GlobalKey<FormState>();
  final _fileName = TextEditingController();
  final _amount = TextEditingController();
  late MediaFormat _format;

  List<MediaFormat> get _formats =>
      widget.isVideo ? MediaFormat.videoFormats : MediaFormat.imageFormats;

  @override
  void initState() {
    super.initState();
    _format = _formats.first;
  }

  @override
  void dispose() {
    _fileName.dispose();
    _amount.dispose();
    super.dispose();
  }

  void _submit() {
    if (!_formKey.currentState!.validate()) return;
    Navigator.pop(
      context,
      MediaInput(
        fileName: _fileName.text.trim(),
        format: _format,
        amount: int.parse(_amount.text.trim()),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: Text(widget.isVideo ? 'Dodaj wideo' : 'Dodaj zdjęcie'),
      content: Form(
        key: _formKey,
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              TextFormField(
                controller: _fileName,
                decoration: const InputDecoration(labelText: 'Nazwa pliku'),
                validator: FormValidators.fileName,
              ),
              const SizedBox(height: 12),
              DropdownButtonFormField<MediaFormat>(
                value: _format,
                decoration: const InputDecoration(labelText: 'Format'),
                items: [
                  for (final f in _formats)
                    DropdownMenuItem(value: f, child: Text(f.label)),
                ],
                onChanged: (v) => setState(() => _format = v ?? _format),
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _amount,
                keyboardType: TextInputType.number,
                decoration: InputDecoration(
                  labelText:
                      widget.isVideo ? 'Długość (sekundy)' : 'Rozmiar (KB)',
                ),
                validator: (v) => FormValidators.positiveInt(
                  v,
                  widget.isVideo ? 'Długość' : 'Rozmiar',
                ),
              ),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(context),
          child: const Text('Anuluj'),
        ),
        FilledButton(onPressed: _submit, child: const Text('Dodaj')),
      ],
    );
  }
}

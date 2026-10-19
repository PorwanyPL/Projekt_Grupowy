import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/manager_user.dart';
import '../providers/manager_provider.dart';
import '../services/api_exception.dart';
import '../utils/validators.dart';

class ManagerFormScreen extends StatefulWidget {
  const ManagerFormScreen({super.key, this.manager});

  /// null = tworzenie, w przeciwnym razie edycja.
  final ManagerUser? manager;

  @override
  State<ManagerFormScreen> createState() => _ManagerFormScreenState();
}

class _ManagerFormScreenState extends State<ManagerFormScreen> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _firstName;
  late final TextEditingController _lastName;
  late final TextEditingController _age;
  late final TextEditingController _description;

  bool _saving = false;
  String? _error;

  bool get _isEdit => widget.manager != null;

  @override
  void initState() {
    super.initState();
    final m = widget.manager;
    _firstName = TextEditingController(text: m?.firstName ?? '');
    _lastName = TextEditingController(text: m?.lastName ?? '');
    _age = TextEditingController(text: m?.age.toString() ?? '');
    _description = TextEditingController(text: m?.description ?? '');
  }

  @override
  void dispose() {
    _firstName.dispose();
    _lastName.dispose();
    _age.dispose();
    _description.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() {
      _saving = true;
      _error = null;
    });

    final input = ManagerInput(
      firstName: _firstName.text.trim(),
      lastName: _lastName.text.trim(),
      age: int.parse(_age.text.trim()),
      description: _description.text.trim(),
    );
    final provider = context.read<ManagerProvider>();

    try {
      if (_isEdit) {
        await provider.update(widget.manager!.id, input);
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
    return Scaffold(
      appBar: AppBar(
        title: Text(_isEdit ? 'Edytuj managera' : 'Nowy manager'),
      ),
      body: Form(
        key: _formKey,
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            TextFormField(
              controller: _firstName,
              decoration: const InputDecoration(labelText: 'Imię'),
              textInputAction: TextInputAction.next,
              validator: (v) => FormValidators.name(v, 'Imię'),
            ),
            const SizedBox(height: 16),
            TextFormField(
              controller: _lastName,
              decoration: const InputDecoration(labelText: 'Nazwisko'),
              textInputAction: TextInputAction.next,
              validator: (v) => FormValidators.name(v, 'Nazwisko'),
            ),
            const SizedBox(height: 16),
            TextFormField(
              controller: _age,
              decoration: const InputDecoration(labelText: 'Wiek'),
              keyboardType: TextInputType.number,
              textInputAction: TextInputAction.next,
              validator: FormValidators.age,
            ),
            const SizedBox(height: 16),
            TextFormField(
              controller: _description,
              decoration: const InputDecoration(
                labelText: 'Opis',
                alignLabelWithHint: true,
              ),
              maxLines: 4,
              validator: (v) => FormValidators.text(v, 'Opis', 2000),
            ),
            if (_error != null) ...[
              const SizedBox(height: 16),
              Text(
                _error!,
                style: TextStyle(color: Theme.of(context).colorScheme.error),
              ),
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
                  : Text(_isEdit ? 'Zapisz zmiany' : 'Dodaj managera'),
            ),
          ],
        ),
      ),
    );
  }
}

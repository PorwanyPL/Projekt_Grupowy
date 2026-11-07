import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../providers/client_provider.dart';
import '../services/api_exception.dart';
import '../utils/validators.dart';

/// CompanyUser: dodaje do firmy klienta, który wcześniej sam się zarejestrował (po adresie e-mail).
class ClientLinkScreen extends StatefulWidget {
  const ClientLinkScreen({super.key});

  @override
  State<ClientLinkScreen> createState() => _ClientLinkScreenState();
}

class _ClientLinkScreenState extends State<ClientLinkScreen> {
  final _formKey = GlobalKey<FormState>();
  final _email = TextEditingController();

  bool _saving = false;
  String? _error;

  @override
  void dispose() {
    _email.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() {
      _saving = true;
      _error = null;
    });
    final provider = context.read<ClientProvider>();
    try {
      await provider.link(_email.text.trim());
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
    final scheme = Theme.of(context).colorScheme;

    return Scaffold(
      appBar: AppBar(title: const Text('Dodaj klienta')),
      body: Form(
        key: _formKey,
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            Text(
              'Klient zakłada konto samodzielnie (przycisk "Zarejestruj się jako klient" '
              'na ekranie logowania). Podaj adres e-mail, którego użył przy rejestracji. '
              'Ten sam klient może być powiązany z wieloma firmami.',
              style: Theme.of(context).textTheme.bodySmall,
            ),
            const SizedBox(height: 16),
            TextFormField(
              controller: _email,
              decoration: const InputDecoration(labelText: 'Adres e-mail klienta'),
              keyboardType: TextInputType.emailAddress,
              onFieldSubmitted: (_) => _saving ? null : _submit(),
              validator: FormValidators.email,
            ),
            if (_error != null) ...[
              const SizedBox(height: 16),
              Text(_error!, style: TextStyle(color: scheme.error)),
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
                  : const Text('Powiąż klienta z firmą'),
            ),
          ],
        ),
      ),
    );
  }
}

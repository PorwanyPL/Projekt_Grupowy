import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/auth.dart';
import '../providers/auth_provider.dart';
import '../services/api_exception.dart';
import '../utils/validators.dart';

class RegisterScreen extends StatefulWidget {
  const RegisterScreen({super.key});

  @override
  State<RegisterScreen> createState() => _RegisterScreenState();
}

class _RegisterScreenState extends State<RegisterScreen> {
  final _formKey = GlobalKey<FormState>();
  final _firstName = TextEditingController();
  final _lastName = TextEditingController();
  final _age = TextEditingController();
  final _description = TextEditingController();
  final _email = TextEditingController();
  final _password = TextEditingController();
  final _confirm = TextEditingController();

  bool _loading = false;
  bool _obscure = true;
  String? _error;

  @override
  void dispose() {
    for (final c in [
      _firstName,
      _lastName,
      _age,
      _description,
      _email,
      _password,
      _confirm,
    ]) {
      c.dispose();
    }
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() {
      _loading = true;
      _error = null;
    });

    final navigator = Navigator.of(context);
    final auth = context.read<AuthProvider>();
    try {
      await auth.register(RegisterInput(
        firstName: _firstName.text.trim(),
        lastName: _lastName.text.trim(),
        age: int.parse(_age.text.trim()),
        description: _description.text.trim(),
        email: _email.text.trim(),
        password: _password.text,
      ));
      // Rejestracja loguje od razu; zamykamy formularz, a AuthGate pokaże HomeScreen.
      navigator.popUntil((route) => route.isFirst);
    } on ApiException catch (e) {
      if (mounted) {
        setState(() {
          _loading = false;
          _error = e.message;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;

    return Scaffold(
      appBar: AppBar(title: const Text('Rejestracja')),
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
              maxLines: 3,
              validator: (v) => FormValidators.text(v, 'Opis', 2000),
            ),
            const SizedBox(height: 16),
            TextFormField(
              controller: _email,
              decoration: const InputDecoration(labelText: 'Adres e-mail'),
              keyboardType: TextInputType.emailAddress,
              textInputAction: TextInputAction.next,
              validator: FormValidators.email,
            ),
            const SizedBox(height: 16),
            TextFormField(
              controller: _password,
              decoration: InputDecoration(
                labelText: 'Hasło',
                helperText: '8-128 znaków, mała i wielka litera oraz cyfra',
                suffixIcon: IconButton(
                  tooltip: _obscure ? 'Pokaż hasło' : 'Ukryj hasło',
                  icon: Icon(_obscure ? Icons.visibility : Icons.visibility_off),
                  onPressed: () => setState(() => _obscure = !_obscure),
                ),
              ),
              obscureText: _obscure,
              textInputAction: TextInputAction.next,
              validator: FormValidators.newPassword,
            ),
            const SizedBox(height: 16),
            TextFormField(
              controller: _confirm,
              decoration: const InputDecoration(labelText: 'Powtórz hasło'),
              obscureText: _obscure,
              validator: (v) => FormValidators.confirmPassword(v, _password.text),
            ),
            if (_error != null) ...[
              const SizedBox(height: 16),
              Text(_error!, style: TextStyle(color: scheme.error)),
            ],
            const SizedBox(height: 24),
            FilledButton(
              onPressed: _loading ? null : _submit,
              child: _loading
                  ? const SizedBox(
                      height: 20,
                      width: 20,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Text('Utwórz konto'),
            ),
          ],
        ),
      ),
    );
  }
}

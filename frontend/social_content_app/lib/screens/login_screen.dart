import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';

import '../models/auth.dart';
import '../providers/auth_provider.dart';
import '../services/api_exception.dart';
import '../utils/validators.dart';
import 'register_client_screen.dart';
import 'register_company_screen.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _formKey = GlobalKey<FormState>();
  final _email = TextEditingController();
  final _password = TextEditingController();
  final _code = TextEditingController();

  /// Ustawione po poprawnym haśle, gdy konto ma weryfikację dwuetapową: ekran pokazuje pole na kod z e-maila.
  TwoFactorChallenge? _challenge;

  bool _loading = false;
  bool _obscure = true;
  String? _error;

  @override
  void dispose() {
    _email.dispose();
    _password.dispose();
    _code.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final challenge = await context.read<AuthProvider>().login(_email.text, _password.text);
      // Bez 2FA AuthGate sam przełączy ekran na HomeScreen; z 2FA pokazujemy krok z kodem.
      if (challenge != null && mounted) {
        setState(() {
          _challenge = challenge;
          _loading = false;
          _code.clear();
        });
      }
    } on ApiException catch (e) {
      if (mounted) {
        setState(() {
          _loading = false;
          _error = e.message;
        });
      }
    }
  }

  Future<void> _submitCode() async {
    final challenge = _challenge;
    if (challenge == null) return;
    final code = _code.text.trim();
    if (!RegExp(r'^\d{6}$').hasMatch(code)) {
      setState(() => _error = 'Kod składa się z 6 cyfr.');
      return;
    }
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      await context.read<AuthProvider>().verifyCode(challenge.challengeId, code);
    } on ApiException catch (e) {
      if (mounted) {
        setState(() {
          _loading = false;
          _error = e.message;
        });
      }
    }
  }

  void _backToPassword() => setState(() {
        _challenge = null;
        _error = null;
        _loading = false;
        _code.clear();
      });

  Widget _buildCodeStep(ColorScheme scheme) {
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Text('Weryfikacja dwuetapowa', style: Theme.of(context).textTheme.titleMedium),
        const SizedBox(height: 8),
        Text('Wysłaliśmy 6-cyfrowy kod na adres ${_challenge!.maskedEmail}. Kod jest ważny 5 minut.'),
        const SizedBox(height: 16),
        TextField(
          key: const Key('two-factor-code'),
          controller: _code,
          autofocus: true,
          keyboardType: TextInputType.number,
          maxLength: 6,
          inputFormatters: [FilteringTextInputFormatter.digitsOnly],
          decoration: const InputDecoration(labelText: 'Kod z e-maila'),
          onSubmitted: (_) => _loading ? null : _submitCode(),
        ),
        if (_error != null) ...[
          const SizedBox(height: 8),
          Text(_error!, style: TextStyle(color: scheme.error)),
        ],
        const SizedBox(height: 16),
        FilledButton(
          key: const Key('two-factor-submit'),
          onPressed: _loading ? null : _submitCode,
          child: _loading
              ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
              : const Text('Zweryfikuj'),
        ),
        const SizedBox(height: 8),
        TextButton(
          onPressed: _loading ? null : _backToPassword,
          child: const Text('Wróć (nowy kod dostaniesz po ponownym logowaniu)'),
        ),
      ],
    );
  }

  @override
  Widget build(BuildContext context) {
    final notice = context.watch<AuthProvider>().notice;
    final scheme = Theme.of(context).colorScheme;

    return Scaffold(
      appBar: AppBar(title: const Text('Logowanie')),
      body: _challenge != null
          ? _buildCodeStep(scheme)
          : Form(
        key: _formKey,
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            if (notice != null) ...[
              Card(
                color: scheme.secondaryContainer,
                child: Padding(
                  padding: const EdgeInsets.all(12),
                  child: Text(notice),
                ),
              ),
              const SizedBox(height: 16),
            ],
            TextFormField(
              controller: _email,
              decoration: const InputDecoration(labelText: 'Adres e-mail'),
              keyboardType: TextInputType.emailAddress,
              autofillHints: const [AutofillHints.email],
              textInputAction: TextInputAction.next,
              validator: FormValidators.email,
            ),
            const SizedBox(height: 16),
            TextFormField(
              controller: _password,
              decoration: InputDecoration(
                labelText: 'Hasło',
                suffixIcon: IconButton(
                  tooltip: _obscure ? 'Pokaż hasło' : 'Ukryj hasło',
                  icon: Icon(_obscure ? Icons.visibility : Icons.visibility_off),
                  onPressed: () => setState(() => _obscure = !_obscure),
                ),
              ),
              obscureText: _obscure,
              autofillHints: const [AutofillHints.password],
              onFieldSubmitted: (_) => _loading ? null : _submit(),
              validator: (v) => FormValidators.required(v, 'Hasło'),
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
                  : const Text('Zaloguj się'),
            ),
            const SizedBox(height: 24),
            const Divider(),
            const SizedBox(height: 8),
            Text(
              'Nie masz konta?',
              textAlign: TextAlign.center,
              style: Theme.of(context).textTheme.titleSmall,
            ),
            const SizedBox(height: 8),
            OutlinedButton.icon(
              onPressed: _loading
                  ? null
                  : () => Navigator.push(
                        context,
                        MaterialPageRoute(
                            builder: (_) => const RegisterCompanyScreen()),
                      ),
              icon: const Icon(Icons.business),
              label: const Text('Zarejestruj firmę'),
            ),
            const SizedBox(height: 8),
            OutlinedButton.icon(
              onPressed: _loading
                  ? null
                  : () => Navigator.push(
                        context,
                        MaterialPageRoute(
                            builder: (_) => const RegisterClientScreen()),
                      ),
              icon: const Icon(Icons.person_add_alt),
              label: const Text('Zarejestruj się jako klient'),
            ),
            const SizedBox(height: 8),
            Text(
              'Managerów tworzy administrator firmy - po otrzymaniu e-maila i hasła zaloguj się powyżej.',
              textAlign: TextAlign.center,
              style: Theme.of(context).textTheme.bodySmall,
            ),
          ],
        ),
      ),
    );
  }
}

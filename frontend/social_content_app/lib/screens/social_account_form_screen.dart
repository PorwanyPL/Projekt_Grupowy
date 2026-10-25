import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/enums.dart';
import '../models/social_account.dart';
import '../providers/manager_provider.dart';
import '../providers/social_account_provider.dart';
import '../services/api_exception.dart';
import '../utils/formatters.dart';
import '../utils/validators.dart';

class SocialAccountFormScreen extends StatefulWidget {
  const SocialAccountFormScreen({super.key, this.account});

  /// null = tworzenie, w przeciwnym razie edycja.
  final SocialAccount? account;

  @override
  State<SocialAccountFormScreen> createState() => _SocialAccountFormScreenState();
}

class _SocialAccountFormScreenState extends State<SocialAccountFormScreen> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _accountId;
  late final TextEditingController _externalId;
  late final TextEditingController _name;
  final _accessToken = TextEditingController();
  final _refreshToken = TextEditingController();

  late SocialPlatform _platform;
  late DateTime _expiresAt;
  late bool _isActive;
  late final Set<String> _managerIds;

  bool _saving = false;
  String? _error;

  bool get _isEdit => widget.account != null;

  @override
  void initState() {
    super.initState();
    final a = widget.account;
    _accountId = TextEditingController(text: a?.accountId ?? '');
    _externalId = TextEditingController(text: a?.externalPlatformId ?? '');
    _name = TextEditingController(text: a?.accountName ?? '');
    _platform = a?.platform ?? SocialPlatform.facebook;
    _expiresAt = a?.tokenExpiresAt ?? DateTime.now().add(const Duration(days: 60));
    _isActive = a?.isActive ?? true;
    _managerIds = {...?a?.managers.map((m) => m.id)};
  }

  @override
  void dispose() {
    _accountId.dispose();
    _externalId.dispose();
    _name.dispose();
    _accessToken.dispose();
    _refreshToken.dispose();
    super.dispose();
  }

  Future<void> _pickDate() async {
    final now = DateTime.now();
    var first = DateTime(now.year - 1);
    if (_expiresAt.isBefore(first)) first = DateTime(_expiresAt.year, _expiresAt.month, _expiresAt.day);
    var last = DateTime(now.year + 10);
    if (_expiresAt.isAfter(last)) last = _expiresAt;
    final picked = await showDatePicker(
      context: context,
      initialDate: _expiresAt,
      firstDate: first,
      lastDate: last,
    );
    if (picked != null) {
      setState(() => _expiresAt = DateTime(picked.year, picked.month, picked.day, 23, 59));
    }
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() {
      _saving = true;
      _error = null;
    });

    String? optional(TextEditingController c) =>
        c.text.trim().isEmpty ? null : c.text.trim();

    final input = SocialAccountInput(
      accountId: _accountId.text.trim(),
      platform: _platform,
      externalPlatformId: _externalId.text.trim(),
      accountName: _name.text.trim(),
      accessToken: optional(_accessToken),
      refreshToken: optional(_refreshToken),
      tokenExpiresAt: _expiresAt,
      isActive: _isActive,
      managerUserIds: _managerIds.toList(),
    );
    final provider = context.read<SocialAccountProvider>();
    final managers = context.read<ManagerProvider>();

    try {
      if (_isEdit) {
        await provider.update(widget.account!.id, input);
      } else {
        await provider.create(input);
      }
      await managers.load(); // przypisania managerów do konta się zmieniły
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
    final scheme = Theme.of(context).colorScheme;

    return Scaffold(
      appBar: AppBar(
        title: Text(_isEdit ? 'Edytuj konto' : 'Nowe konto społecznościowe'),
      ),
      body: Form(
        key: _formKey,
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            DropdownButtonFormField<SocialPlatform>(
              value: _platform,
              decoration: const InputDecoration(labelText: 'Platforma'),
              items: [
                for (final p in SocialPlatform.values)
                  DropdownMenuItem(value: p, child: Text(p.label)),
              ],
              onChanged: (v) => setState(() => _platform = v ?? _platform),
            ),
            const SizedBox(height: 16),
            TextFormField(
              controller: _name,
              decoration: const InputDecoration(labelText: 'Nazwa konta'),
              textInputAction: TextInputAction.next,
              validator: (v) => FormValidators.text(v, 'Nazwa konta', 150),
            ),
            const SizedBox(height: 16),
            TextFormField(
              controller: _accountId,
              decoration: const InputDecoration(
                labelText: 'Identyfikator konta w systemie',
                helperText: 'Unikalny w obrębie platformy',
              ),
              textInputAction: TextInputAction.next,
              validator: (v) => FormValidators.text(v, 'Identyfikator konta', 100),
            ),
            const SizedBox(height: 16),
            TextFormField(
              controller: _externalId,
              decoration: const InputDecoration(
                labelText: 'Identyfikator po stronie platformy',
              ),
              textInputAction: TextInputAction.next,
              validator: (v) => FormValidators.text(v, 'Identyfikator platformy', 100),
            ),
            const SizedBox(height: 16),
            TextFormField(
              controller: _accessToken,
              decoration: InputDecoration(
                labelText: 'Token dostępu',
                helperText: _isEdit ? 'Zostaw puste, aby nie zmieniać' : null,
              ),
              obscureText: true,
              textInputAction: TextInputAction.next,
              validator: (v) {
                final t = v?.trim() ?? '';
                if (t.isEmpty) return _isEdit ? null : 'Token dostępu jest wymagany';
                if (t.length > 2000) return 'Maksymalnie 2000 znaków';
                return null;
              },
            ),
            const SizedBox(height: 16),
            TextFormField(
              controller: _refreshToken,
              decoration: InputDecoration(
                labelText: 'Token odświeżania (opcjonalnie)',
                helperText: _isEdit && widget.account!.hasRefreshToken
                    ? 'Zapisany - zostaw puste, aby nie zmieniać'
                    : null,
              ),
              obscureText: true,
              validator: (v) =>
                  (v?.trim().length ?? 0) > 2000 ? 'Maksymalnie 2000 znaków' : null,
            ),
            const SizedBox(height: 16),
            ListTile(
              contentPadding: EdgeInsets.zero,
              title: const Text('Token ważny do'),
              subtitle: Text(formatDateTime(_expiresAt)),
              trailing: const Icon(Icons.calendar_today),
              onTap: _pickDate,
            ),
            SwitchListTile(
              contentPadding: EdgeInsets.zero,
              title: const Text('Konto aktywne'),
              value: _isActive,
              onChanged: (v) => setState(() => _isActive = v),
            ),
            const SizedBox(height: 8),
            Text('Managerowie konta', style: Theme.of(context).textTheme.titleSmall),
            if (managers.isEmpty)
              const Padding(
                padding: EdgeInsets.symmetric(vertical: 8),
                child: Text('Brak managerów do przypisania.'),
              )
            else
              Wrap(
                spacing: 8,
                children: [
                  for (final m in managers)
                    FilterChip(
                      label: Text(m.fullName),
                      selected: _managerIds.contains(m.id),
                      onSelected: (on) => setState(() {
                        on ? _managerIds.add(m.id) : _managerIds.remove(m.id);
                      }),
                    ),
                ],
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
                  : Text(_isEdit ? 'Zapisz zmiany' : 'Dodaj konto'),
            ),
          ],
        ),
      ),
    );
  }
}

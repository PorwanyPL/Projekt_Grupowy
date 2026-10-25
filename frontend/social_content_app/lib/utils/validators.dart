/// Walidatory formularzy - odpowiadają regułom domeny z backendu.
class FormValidators {
  static final RegExp _name =
      RegExp(r"^\p{L}(?:[\p{L}'\-]*\p{L})?$", unicode: true);

  static String? name(String? value, String field) {
    final v = value?.trim() ?? '';
    if (v.isEmpty) return '$field jest wymagane';
    if (v.length > 50) return '$field może mieć maksymalnie 50 znaków';
    if (!_name.hasMatch(v)) {
      return '$field: tylko litery, "-" i apostrof (zaczyna i kończy się literą)';
    }
    return null;
  }

  static String? age(String? value) {
    final n = int.tryParse(value?.trim() ?? '');
    if (n == null) return 'Podaj wiek jako liczbę';
    if (n < 18 || n > 100) return 'Wiek musi mieścić się w przedziale 18-100';
    return null;
  }

  static String? text(String? value, String field, int max) {
    final v = value?.trim() ?? '';
    if (v.isEmpty) return '$field jest wymagany';
    if (v.length > max) return '$field może mieć maksymalnie $max znaków';
    return null;
  }

  static String? fileName(String? value) {
    final v = value?.trim() ?? '';
    if (v.isEmpty) return 'Nazwa pliku jest wymagana';
    if (v.length > 150) return 'Maksymalnie 150 znaków';
    if (RegExp(r'[\\/:*?"<>|]').hasMatch(v)) {
      return 'Nazwa zawiera niedozwolone znaki';
    }
    return null;
  }

  static String? positiveInt(String? value, String field) {
    final n = int.tryParse(value?.trim() ?? '');
    if (n == null || n <= 0) return '$field musi być liczbą większą od 0';
    return null;
  }

  static String? email(String? value) {
    final v = value?.trim() ?? '';
    if (v.isEmpty) return 'Adres e-mail jest wymagany';
    if (v.length > 254) return 'Adres e-mail może mieć maksymalnie 254 znaki';
    if (!RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$').hasMatch(v)) {
      return 'Podaj poprawny adres e-mail';
    }
    return null;
  }

  /// Hasło przy rejestracji: 8-128 znaków, mała i wielka litera oraz cyfra (jak w backendzie).
  static String? newPassword(String? value) {
    final v = value ?? '';
    if (v.isEmpty) return 'Hasło jest wymagane';
    final problems = <String>[];
    if (v.length < 8 || v.length > 128) problems.add('długość 8-128 znaków');
    if (!RegExp(r'\p{Ll}', unicode: true).hasMatch(v)) problems.add('mała litera');
    if (!RegExp(r'\p{Lu}', unicode: true).hasMatch(v)) problems.add('wielka litera');
    if (!RegExp(r'\d').hasMatch(v)) problems.add('cyfra');
    if (problems.isEmpty) return null;
    return 'Hasło wymaga: ${problems.join(', ')}';
  }

  static String? required(String? value, String field) =>
      (value == null || value.isEmpty) ? '$field jest wymagane' : null;

  static String? confirmPassword(String? value, String original) =>
      value != original ? 'Hasła nie są takie same' : null;
}

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
}

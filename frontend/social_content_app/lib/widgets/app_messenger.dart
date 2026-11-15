import 'dart:async';

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

/// Jedno miejsce na komunikaty (zamiast SnackBarów, które zapełniały ekran przy wielokrotnych kliknięciach).
///  - identyczny komunikat, który już jest na ekranie, nie jest dodawany drugi raz (tylko przedłuża się jego czas),
///  - na ekranie jest maks. [maxVisible] komunikatów; starsze znikają,
///  - kolejne różne komunikaty układają się w stos od dołu do góry (najnowszy na dole).
class AppMessenger extends ChangeNotifier {
  AppMessenger({this.maxVisible = 3, this.lifetime = const Duration(seconds: 5)});

  final int maxVisible;
  final Duration lifetime;

  final List<_Entry> _entries = [];
  int _seq = 0;

  /// Komunikaty od najstarszego do najnowszego.
  List<String> get messages => [for (final e in _entries) e.text];

  void show(String text) {
    final existing = _entries.where((e) => e.text == text).firstOrNull;
    if (existing != null) {
      existing.restart(() => _remove(existing.id), lifetime);
      return;
    }
    final entry = _Entry(++_seq, text);
    entry.restart(() => _remove(entry.id), lifetime);
    _entries.add(entry);
    while (_entries.length > maxVisible) {
      _entries.removeAt(0).cancel();
    }
    notifyListeners();
  }

  void dismiss(String text) {
    final e = _entries.where((e) => e.text == text).firstOrNull;
    if (e != null) _remove(e.id);
  }

  void clear() {
    for (final e in _entries) {
      e.cancel();
    }
    _entries.clear();
    notifyListeners();
  }

  void _remove(int id) {
    final e = _entries.where((e) => e.id == id).firstOrNull;
    if (e == null) return;
    e.cancel();
    _entries.remove(e);
    notifyListeners();
  }

  @override
  void dispose() {
    for (final e in _entries) {
      e.cancel();
    }
    super.dispose();
  }
}

class _Entry {
  _Entry(this.id, this.text);

  final int id;
  final String text;
  Timer? _timer;

  void restart(VoidCallback onExpire, Duration lifetime) {
    _timer?.cancel();
    _timer = Timer(lifetime, onExpire);
  }

  void cancel() => _timer?.cancel();
}

/// Warstwa komunikatów nad całą aplikacją (podpięta w MaterialApp.builder), tuż nad dolnym menu.
class AppMessageHost extends StatelessWidget {
  const AppMessageHost({super.key, required this.child, this.bottomInset = 88});

  final Widget child;

  /// Odstęp od dołu, żeby komunikaty nie zasłaniały dolnego menu.
  final double bottomInset;

  @override
  Widget build(BuildContext context) {
    final messages = context.watch<AppMessenger>().messages;
    final scheme = Theme.of(context).colorScheme;
    return Stack(
      children: [
        child,
        if (messages.isNotEmpty)
          Positioned(
            left: 16,
            right: 16,
            bottom: bottomInset,
            child: Align(
              alignment: Alignment.bottomCenter,
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 560),
                child: Column(
                  key: const Key('app-messages'),
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    for (final m in messages)
                      Padding(
                        padding: const EdgeInsets.only(top: 6),
                        child: Material(
                          color: scheme.inverseSurface,
                          elevation: 6,
                          borderRadius: BorderRadius.circular(8),
                          child: InkWell(
                            borderRadius: BorderRadius.circular(8),
                            onTap: () => context.read<AppMessenger>().dismiss(m),
                            child: Padding(
                              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
                              child: Row(
                                children: [
                                  Expanded(
                                    child: Text(m, style: TextStyle(color: scheme.onInverseSurface)),
                                  ),
                                  Icon(Icons.close, size: 16, color: scheme.onInverseSurface),
                                ],
                              ),
                            ),
                          ),
                        ),
                      ),
                  ],
                ),
              ),
            ),
          ),
      ],
    );
  }
}

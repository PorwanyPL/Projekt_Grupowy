import 'dart:async';

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

/// Obserwator tras jednego Navigatora (zakładki w HomeScreen) - daje znać, kiedy ekran zostaje otwarty albo
/// odsłonięty po zamknięciu ekranu nad nim. Każdy Navigator musi mieć WŁASNEGO obserwatora (jeden obserwator
/// nie może być podpięty pod kilka Navigatorów), więc HomeScreen udostępnia go ekranom przez ten widget.
class RouteObserverScope extends InheritedWidget {
  const RouteObserverScope({super.key, required this.observer, required super.child});

  final RouteObserver<ModalRoute<void>> observer;

  static RouteObserver<ModalRoute<void>>? maybeOf(BuildContext context) =>
      context.dependOnInheritedWidgetOfExactType<RouteObserverScope>()?.observer;

  @override
  bool updateShouldNotify(RouteObserverScope old) => observer != old.observer;
}

/// Sygnał "zmieniono zakładkę": odświeża widoczne listy (HomeScreen wywołuje [notifyTabChanged]).
class RefreshBus extends ChangeNotifier {
  void notifyTabChanged() => notifyListeners();
}

/// Odświeża dane ekranu: przy wejściu na ekran (przejście z widoku do widoku), po powrocie z ekranu
/// otwartego nad nim, przy zmianie zakładki oraz co [interval] (domyślnie raz na minutę - polling bazy).
/// Pierwsze ładowanie ekranów głównych zakładek robi HomeScreen, więc dla nich start jest pomijany.
class AutoRefresh extends StatefulWidget {
  const AutoRefresh({
    super.key,
    required this.onRefresh,
    required this.child,
    this.interval = const Duration(minutes: 1),
  });

  /// Powinno odświeżać "po cichu" (bez spinnera), żeby widok nie mrugał.
  final FutureOr<void> Function() onRefresh;
  final Widget child;
  final Duration interval;

  @override
  State<AutoRefresh> createState() => _AutoRefreshState();
}

class _AutoRefreshState extends State<AutoRefresh> with RouteAware {
  Timer? _timer;
  ModalRoute<void>? _route;
  RouteObserver<ModalRoute<void>>? _observer;
  RefreshBus? _bus;

  @override
  void initState() {
    super.initState();
    _timer = Timer.periodic(widget.interval, (_) => _refresh());
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    final route = ModalRoute.of(context);
    final observer = RouteObserverScope.maybeOf(context);
    if (route != null && observer != null && (route != _route || observer != _observer)) {
      _observer?.unsubscribe(this);
      _route = route;
      _observer = observer;
      observer.subscribe(this, route);
    } else if (route != null) {
      _route ??= route; // bez obserwatora (np. w testach) działa tylko timer i sygnał zmiany zakładki
    }
    final bus = context.read<RefreshBus>();
    if (bus != _bus) {
      _bus?.removeListener(_refresh);
      _bus = bus..addListener(_refresh);
    }
  }

  @override
  void dispose() {
    _timer?.cancel();
    _observer?.unsubscribe(this);
    _bus?.removeListener(_refresh);
    super.dispose();
  }

  void _refresh() {
    if (!mounted) return;
    // Odświeżamy tylko ekran na wierzchu swojego Navigatora (pod innym ekranem robi to didPopNext).
    if (_route != null && !_route!.isCurrent) return;
    widget.onRefresh();
  }

  /// didPush/didPopNext przychodzą w trakcie budowania drzewa (zmiana trasy), a odświeżenie wywołuje
  /// notifyListeners/setState - to wolno dopiero po zakończeniu klatki (inaczej asercja '!_dirty').
  void _refreshAfterFrame() {
    WidgetsBinding.instance.addPostFrameCallback((_) => _refresh());
  }

  @override
  void didPush() {
    if (_route != null && !_route!.isFirst) _refreshAfterFrame();
  }

  @override
  void didPopNext() => _refreshAfterFrame();

  @override
  Widget build(BuildContext context) => widget.child;
}

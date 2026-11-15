import 'package:flutter/foundation.dart';

import '../models/app_notification.dart';
import '../services/api_exception.dart';
import '../services/notification_service.dart';

/// Powiadomienia zalogowanego użytkownika (każda rola). Dzwonek w górnym menu pokazuje liczbę nieprzeczytanych.
class NotificationProvider extends ChangeNotifier {
  NotificationProvider(this._service);

  final NotificationService _service;

  List<AppNotification> items = const [];
  String? error;

  int get unread => items.where((n) => !n.isRead).length;

  Future<void> load() async {
    try {
      items = await _service.getAll();
      error = null;
    } on ApiException catch (e) {
      error = e.message;
    }
    notifyListeners();
  }

  /// Powiadomienie dostarczone na żywo przez SignalR (bez czekania na polling).
  void push(AppNotification n) {
    if (items.any((e) => e.id == n.id)) return;
    items = [n, ...items];
    notifyListeners();
  }

  Future<void> markAllRead() async {
    try {
      await _service.markAllRead();
    } on ApiException catch (e) {
      error = e.message;
    }
    await load();
  }

  void clear() {
    items = const [];
    error = null;
    notifyListeners();
  }
}

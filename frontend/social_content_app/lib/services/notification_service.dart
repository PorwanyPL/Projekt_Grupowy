import '../models/app_notification.dart';
import 'api_client.dart';

class NotificationService {
  NotificationService(this._api);

  final ApiClient _api;

  Future<List<AppNotification>> getAll() async {
    final json = await _api.get('/api/Notifications') as List<dynamic>;
    return json.map((e) => AppNotification.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<void> markAllRead() => _api.put('/api/Notifications/read-all', const {});
}

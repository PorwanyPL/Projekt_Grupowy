import 'package:flutter/foundation.dart';

/// Prośby o przejście w obrębie ekranu głównego (np. kliknięcie powiadomienia o wiadomości otwiera rozmowę w zakładce Czat).
/// HomeScreen nasłuchuje i przełącza zakładkę; dzwonek (widgets/account_menu.dart) tylko zgłasza prośbę.
class HomeNavigation extends ChangeNotifier {
  String? _pendingConversationId;

  /// Prosi o otwarcie rozmowy czatu o podanym identyfikatorze.
  void openChat(String conversationId) {
    _pendingConversationId = conversationId;
    notifyListeners();
  }

  /// Odbiera (i czyści) oczekującą prośbę o otwarcie rozmowy.
  String? takePendingConversation() {
    final id = _pendingConversationId;
    _pendingConversationId = null;
    return id;
  }
}

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'providers/auth_provider.dart';
import 'providers/manager_provider.dart';
import 'providers/post_provider.dart';
import 'providers/social_account_provider.dart';
import 'screens/auth_gate.dart';
import 'services/api_client.dart';
import 'services/auth_service.dart';
import 'services/manager_service.dart';
import 'services/post_service.dart';
import 'services/social_account_service.dart';
import 'services/token_storage.dart';

final ApiClient _api = ApiClient();
final GlobalKey<NavigatorState> _navigatorKey = GlobalKey<NavigatorState>();

void main() => runApp(const SocialContentApp());

class SocialContentApp extends StatelessWidget {
  const SocialContentApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MultiProvider(
      providers: [
        ChangeNotifierProvider(
          create: (_) => AuthProvider(AuthService(_api), _api, TokenStorage())
            // Po utracie sesji (401, wylogowanie) zamykamy otwarte ekrany - zostaje ekran logowania z AuthGate.
            ..onSessionEnded =
                (() => _navigatorKey.currentState?.popUntil((r) => r.isFirst))
            ..restore(),
        ),
        // Dane są pobierane przez HomeScreen dopiero po zalogowaniu.
        ChangeNotifierProvider(create: (_) => ManagerProvider(ManagerService(_api))),
        ChangeNotifierProvider(create: (_) => PostProvider(PostService(_api))),
        ChangeNotifierProvider(
          create: (_) => SocialAccountProvider(SocialAccountService(_api)),
        ),
      ],
      child: MaterialApp(
        title: 'Social Content',
        navigatorKey: _navigatorKey,
        debugShowCheckedModeBanner: false,
        theme: ThemeData(
          colorSchemeSeed: Colors.indigo,
          useMaterial3: true,
          inputDecorationTheme: const InputDecorationTheme(
            border: OutlineInputBorder(),
          ),
        ),
        home: const AuthGate(),
      ),
    );
  }
}

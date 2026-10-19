import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'providers/manager_provider.dart';
import 'providers/post_provider.dart';
import 'screens/home_screen.dart';
import 'services/api_client.dart';
import 'services/manager_service.dart';
import 'services/post_service.dart';

final ApiClient _api = ApiClient();

void main() => runApp(const SocialContentApp());

class SocialContentApp extends StatelessWidget {
  const SocialContentApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MultiProvider(
      providers: [
        ChangeNotifierProvider(
          create: (_) => ManagerProvider(ManagerService(_api))..load(),
        ),
        ChangeNotifierProvider(
          create: (_) => PostProvider(PostService(_api))..load(),
        ),
      ],
      child: MaterialApp(
        title: 'Social Content',
        debugShowCheckedModeBanner: false,
        theme: ThemeData(
          colorSchemeSeed: Colors.indigo,
          useMaterial3: true,
          inputDecorationTheme: const InputDecorationTheme(
            border: OutlineInputBorder(),
          ),
        ),
        home: const HomeScreen(),
      ),
    );
  }
}

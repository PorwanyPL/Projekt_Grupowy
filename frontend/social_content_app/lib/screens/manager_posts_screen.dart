import 'package:flutter/material.dart';

import '../models/manager_user.dart';
import '../widgets/account_menu.dart';
import '../widgets/posts_view.dart';

/// Widok postów utworzonych przez wskazanego managera (otwierany z listy managerów firmy).
class ManagerPostsScreen extends StatelessWidget {
  const ManagerPostsScreen({super.key, required this.manager});

  final ManagerUser manager;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text('Posty: ${manager.fullName}'), actions: accountMenuActions(context)),
      body: PostsView(managerId: manager.id),
    );
  }
}

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/post.dart';
import '../screens/post_form_screen.dart';
import 'app_messenger.dart';
import 'post_preview_dialog.dart';

/// Otwiera post z listy lub kalendarza tymi samymi zasadami: bez prawa edycji (klient, utrata przypisania) -
/// podgląd tylko do odczytu, post edytowany przez kogoś innego - komunikat, w pozostałych przypadkach formularz
/// edycji (z blokadą PostEditLock). Zwraca true, gdy po powrocie warto odświeżyć dane.
Future<bool> openPost(BuildContext context, Post p) async {
  final messenger = context.read<AppMessenger>();
  if (!p.canEdit) {
    await showDialog<void>(context: context, builder: (ctx) => PostPreviewDialog(post: p));
    return true;
  }
  if (p.lockedByOther) {
    messenger.show('Ten post jest właśnie edytowany przez: ${p.lock!.holderName}. '
        'Spróbuj ponownie za chwilę.');
    return false;
  }
  await Navigator.push(context, MaterialPageRoute(builder: (_) => PostFormScreen(post: p)));
  return true;
}

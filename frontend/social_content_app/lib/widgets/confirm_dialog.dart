import 'package:flutter/material.dart';

Future<bool> confirmDelete(BuildContext context, String what) async {
  final result = await showDialog<bool>(
    context: context,
    builder: (ctx) => AlertDialog(
      title: const Text('Usunąć?'),
      content: Text('Czy na pewno chcesz usunąć: $what? Tej operacji nie można cofnąć.'),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(ctx, false),
          child: const Text('Anuluj'),
        ),
        FilledButton(
          onPressed: () => Navigator.pop(ctx, true),
          child: const Text('Usuń'),
        ),
      ],
    ),
  );
  return result ?? false;
}

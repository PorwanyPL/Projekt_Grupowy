import 'package:flutter/material.dart';

import '../providers/load_state.dart';

/// Wspólna obsługa stanów: ładowanie, błąd (z ponowieniem), pusta lista, dane.
class AsyncBody extends StatelessWidget {
  const AsyncBody({
    super.key,
    required this.state,
    required this.error,
    required this.isEmpty,
    required this.emptyText,
    required this.onRetry,
    required this.builder,
  });

  final LoadState state;
  final String? error;
  final bool isEmpty;
  final String emptyText;
  final Future<void> Function() onRetry;
  final WidgetBuilder builder;

  @override
  Widget build(BuildContext context) {
    switch (state) {
      case LoadState.idle:
      case LoadState.loading:
        return const Center(child: CircularProgressIndicator());
      case LoadState.failed:
        return Center(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Icon(Icons.cloud_off, size: 48),
                const SizedBox(height: 12),
                Text(error ?? 'Wystąpił błąd', textAlign: TextAlign.center),
                const SizedBox(height: 16),
                FilledButton.icon(
                  onPressed: onRetry,
                  icon: const Icon(Icons.refresh),
                  label: const Text('Spróbuj ponownie'),
                ),
              ],
            ),
          ),
        );
      case LoadState.loaded:
        if (isEmpty) {
          return RefreshIndicator(
            onRefresh: onRetry,
            child: ListView(
              children: [
                const SizedBox(height: 120),
                Center(child: Text(emptyText)),
              ],
            ),
          );
        }
        return RefreshIndicator(onRefresh: onRetry, child: builder(context));
    }
  }
}

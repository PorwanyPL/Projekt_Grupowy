import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/chat.dart';
import '../providers/chat_provider.dart';
import '../services/api_exception.dart';
import '../utils/formatters.dart';
import '../widgets/account_menu.dart';
import '../widgets/app_messenger.dart';

/// Okno rozmowy: historia (z bazy) + wiadomości na żywo. Błędy (np. brak uprawnień po zmianie reguł)
/// pokazuje AppMessenger, a wpisany tekst zostaje w polu.
class ChatScreen extends StatefulWidget {
  const ChatScreen({super.key, required this.conversationId});

  final String conversationId;

  @override
  State<ChatScreen> createState() => _ChatScreenState();
}

class _ChatScreenState extends State<ChatScreen> {
  final _input = TextEditingController();
  final _scroll = ScrollController();
  late final ChatProvider _chat;
  bool _loading = true;
  bool _sending = false;
  int _lastCount = 0;

  @override
  void initState() {
    super.initState();
    _chat = context.read<ChatProvider>();
    WidgetsBinding.instance.addPostFrameCallback((_) => _open());
  }

  Future<void> _open() async {
    try {
      await _chat.openConversation(widget.conversationId);
    } on ApiException catch (e) {
      if (mounted) context.read<AppMessenger>().show(e.message);
    }
    if (mounted) setState(() => _loading = false);
  }

  @override
  void dispose() {
    _chat.closeConversation(widget.conversationId);
    _input.dispose();
    _scroll.dispose();
    super.dispose();
  }

  Future<void> _send() async {
    final text = _input.text.trim();
    if (text.isEmpty || _sending) return;
    setState(() => _sending = true);
    try {
      await _chat.send(widget.conversationId, text);
      _input.clear();
    } on ApiException catch (e) {
      if (mounted) context.read<AppMessenger>().show(e.message);
    } finally {
      if (mounted) setState(() => _sending = false);
    }
  }

  void _scrollToEnd() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (_scroll.hasClients) _scroll.jumpTo(_scroll.position.maxScrollExtent);
    });
  }

  @override
  Widget build(BuildContext context) {
    final chat = context.watch<ChatProvider>();
    final conversation = chat.conversationById(widget.conversationId);
    final messages = chat.messagesOf(widget.conversationId);
    if (messages.length != _lastCount) {
      _lastCount = messages.length;
      _scrollToEnd();
    }

    return Scaffold(
      appBar: AppBar(
        title: Text(conversation?.title ?? 'Rozmowa'),
        actions: accountMenuActions(context),
      ),
      body: Column(
        children: [
          Expanded(
            child: _loading
                ? const Center(child: CircularProgressIndicator())
                : messages.isEmpty
                    ? const Center(child: Text('Brak wiadomości. Napisz pierwszą.'))
                    : ListView.builder(
                        key: const Key('chat-messages'),
                        controller: _scroll,
                        padding: const EdgeInsets.all(12),
                        itemCount: messages.length,
                        itemBuilder: (_, i) => _Bubble(
                          message: messages[i],
                          mine: chat.isMine(messages[i]),
                          showName: conversation?.isGroup ?? false,
                        ),
                      ),
          ),
          SafeArea(
            top: false,
            child: Padding(
              padding: const EdgeInsets.fromLTRB(12, 4, 12, 8),
              child: Row(
                children: [
                  Expanded(
                    child: TextField(
                      key: const Key('chat-input'),
                      controller: _input,
                      minLines: 1,
                      maxLines: 4,
                      maxLength: ChatMessageLimits.maxText,
                      textInputAction: TextInputAction.send,
                      onSubmitted: (_) => _send(),
                      decoration: const InputDecoration(hintText: 'Napisz wiadomość...', counterText: ''),
                    ),
                  ),
                  const SizedBox(width: 8),
                  IconButton.filled(
                    key: const Key('chat-send'),
                    tooltip: 'Wyślij',
                    onPressed: _sending ? null : _send,
                    icon: _sending
                        ? const SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2))
                        : const Icon(Icons.send),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _Bubble extends StatelessWidget {
  const _Bubble({required this.message, required this.mine, required this.showName});

  final ChatMessage message;
  final bool mine;
  final bool showName;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Align(
      alignment: mine ? Alignment.centerRight : Alignment.centerLeft,
      child: ConstrainedBox(
        constraints: BoxConstraints(maxWidth: MediaQuery.sizeOf(context).width * 0.8),
        child: Card(
          color: mine ? scheme.primaryContainer : scheme.surfaceContainerHighest,
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                if (showName && !mine)
                  Text(message.senderName,
                      style: Theme.of(context).textTheme.labelMedium?.copyWith(fontWeight: FontWeight.w700)),
                SelectableText(message.text),
                const SizedBox(height: 2),
                Text(formatDateTime(message.sentAt), style: Theme.of(context).textTheme.bodySmall),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

/// Zgodne z ChatMessage.MaxTextLength po stronie serwera.
class ChatMessageLimits {
  static const int maxText = 2000;
}

import 'dart:async';

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/auth.dart';
import '../models/chat.dart';
import '../providers/auth_provider.dart';
import '../providers/chat_provider.dart';
import '../providers/manager_provider.dart';
import '../services/api_exception.dart';
import '../utils/formatters.dart';
import '../widgets/account_menu.dart';
import '../widgets/app_messenger.dart';
import '../widgets/async_body.dart';
import '../widgets/auto_refresh.dart';
import 'chat_screen.dart';

/// Zakładka "Czat": lista rozmów (1 na 1 i grupy firmy). Nowa rozmowa wybiera osobę spośród kontaktów, które
/// serwer dopuszcza dla danej roli; firma i manager mogą też założyć grupę, a firma wskazać managera ds. czatu.
class ChatListScreen extends StatelessWidget {
  const ChatListScreen({super.key});

  Future<void> _newDirect(BuildContext context) async {
    final chat = context.read<ChatProvider>();
    final messenger = context.read<AppMessenger>();
    final picked = await showDialog<ChatContact>(
      context: context,
      builder: (_) => _ContactPickerDialog(search: (phrase, mode) => chat.contacts(search: phrase, mode: mode)),
    );
    if (picked == null || !context.mounted) return;
    try {
      final conversation = await chat.openDirect(picked);
      if (!context.mounted) return;
      await Navigator.push(
          context, MaterialPageRoute(builder: (_) => ChatScreen(conversationId: conversation.id)));
    } on ApiException catch (e) {
      messenger.show(e.message);
    }
  }

  Future<void> _newGroup(BuildContext context) async {
    final chat = context.read<ChatProvider>();
    final messenger = context.read<AppMessenger>();
    try {
      final contacts = (await chat.contacts()).where((c) => c.role == UserRole.managerUser).toList();
      if (!context.mounted) return;
      if (contacts.isEmpty) {
        messenger.show('Brak managerów, których możesz dodać do grupy.');
        return;
      }
      final result = await showDialog<_GroupResult>(
        context: context,
        builder: (_) => _GroupDialog(managers: contacts),
      );
      if (result == null || !context.mounted) return;
      final conversation = await chat.createGroup(result.name, result.managerIds);
      if (!context.mounted) return;
      await Navigator.push(
          context, MaterialPageRoute(builder: (_) => ChatScreen(conversationId: conversation.id)));
    } on ApiException catch (e) {
      messenger.show(e.message);
    }
  }

  Future<void> _chooseChatManager(BuildContext context) async {
    final chat = context.read<ChatProvider>();
    final managers = context.read<ManagerProvider>();
    final messenger = context.read<AppMessenger>();
    try {
      if (managers.items.isEmpty) await managers.load(silent: true);
      final current = await chat.chatManager();
      if (!context.mounted) return;
      final result = await showDialog<_ChatManagerResult>(
        context: context,
        builder: (_) => _ChatManagerDialog(managers: managers.items, currentId: current.managerId),
      );
      if (result == null) return;
      final saved = await chat.setChatManager(result.managerId);
      messenger.show(saved.managerId == null
          ? 'Usunięto managera ds. czatu.'
          : 'Manager ds. czatu: ${saved.managerName ?? ''}'.trim());
    } on ApiException catch (e) {
      messenger.show(e.message);
    }
  }

  @override
  Widget build(BuildContext context) {
    final chat = context.watch<ChatProvider>();
    final role = context.watch<AuthProvider>().role;
    final canGroup = role == UserRole.companyUser || role == UserRole.managerUser;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Czat'),
        actions: [
          IconButton(
            key: const Key('chat-refresh'),
            tooltip: 'Odśwież',
            icon: const Icon(Icons.refresh),
            onPressed: chat.loadConversations,
          ),
          if (role == UserRole.companyUser)
            IconButton(
              key: const Key('chat-manager-button'),
              tooltip: 'Manager ds. czatu',
              icon: const Icon(Icons.support_agent),
              onPressed: () => _chooseChatManager(context),
            ),
          if (canGroup)
            IconButton(
              key: const Key('chat-new-group'),
              tooltip: 'Nowa grupa',
              icon: const Icon(Icons.group_add_outlined),
              onPressed: () => _newGroup(context),
            ),
          ...accountMenuActions(context),
        ],
      ),
      floatingActionButton: FloatingActionButton.extended(
        key: const Key('chat-new-direct'),
        onPressed: () => _newDirect(context),
        icon: const Icon(Icons.chat_outlined),
        label: const Text('Nowa rozmowa'),
      ),
      body: AutoRefresh(
        onRefresh: () => chat.loadConversations(silent: true),
        child: AsyncBody(
          state: chat.state,
          error: chat.error,
          isEmpty: chat.conversations.isEmpty,
          emptyText: 'Brak rozmów. Użyj „Nowa rozmowa”, żeby do kogoś napisać.',
          onRetry: chat.loadConversations,
          builder: (_) => ListView.separated(
            padding: const EdgeInsets.only(bottom: 88),
            itemCount: chat.conversations.length,
            separatorBuilder: (_, __) => const Divider(height: 1),
            itemBuilder: (context, i) => _ConversationTile(conversation: chat.conversations[i]),
          ),
        ),
      ),
    );
  }
}

class _ConversationTile extends StatelessWidget {
  const _ConversationTile({required this.conversation});

  final Conversation conversation;

  @override
  Widget build(BuildContext context) {
    final last = conversation.lastMessage;
    final unread = conversation.unreadCount;
    return ListTile(
      leading: CircleAvatar(child: Icon(conversation.isGroup ? Icons.groups : Icons.person)),
      title: Text(conversation.title,
          style: unread > 0 ? const TextStyle(fontWeight: FontWeight.w700) : null),
      subtitle: Text(
        last == null ? 'Brak wiadomości' : '${last.senderName}: ${last.text}',
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
      ),
      trailing: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        crossAxisAlignment: CrossAxisAlignment.end,
        children: [
          if (last != null) Text(formatDateTime(last.sentAt), style: Theme.of(context).textTheme.bodySmall),
          if (unread > 0) Badge(label: Text('$unread')),
        ],
      ),
      onTap: () => Navigator.push(
          context, MaterialPageRoute(builder: (_) => ChatScreen(conversationId: conversation.id))),
    );
  }
}

/// Wybór rozmówcy z wyszukiwarką: fraza i tryb dopasowania (początek nazwy, fragment nazwy, e-mail) idą do serwera
/// przy każdej zmianie (z krótkim opóźnieniem), a serwer zwraca tylko osoby, z którymi wolno pisać.
class _ContactPickerDialog extends StatefulWidget {
  const _ContactPickerDialog({required this.search});

  final Future<List<ChatContact>> Function(String phrase, ContactSearchMode mode) search;

  @override
  State<_ContactPickerDialog> createState() => _ContactPickerDialogState();
}

class _ContactPickerDialogState extends State<_ContactPickerDialog> {
  final _controller = TextEditingController();
  Timer? _debounce;
  ContactSearchMode _mode = ContactSearchMode.contains;
  List<ChatContact> _contacts = const [];
  bool _loading = true;
  String? _error;
  int _request = 0;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _debounce?.cancel();
    _controller.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    final request = ++_request;
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final list = await widget.search(_controller.text, _mode);
      if (!mounted || request != _request) return; // odpowiedź na starszą frazę
      setState(() {
        _contacts = list;
        _loading = false;
      });
    } on ApiException catch (e) {
      if (!mounted || request != _request) return;
      setState(() {
        _error = e.message;
        _loading = false;
      });
    }
  }

  void _onChanged(String _) {
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 300), _load);
  }

  String get _hint => switch (_mode) {
        ContactSearchMode.contains => 'Fragment imienia, nazwiska lub nazwy',
        ContactSearchMode.email => 'Adres e-mail lub jego fragment',
      };

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: const Text('Z kim chcesz porozmawiać?'),
      content: SizedBox(
        width: 420,
        height: 420,
        child: Column(
          children: [
            TextField(
              key: const Key('chat-search'),
              controller: _controller,
              autofocus: true,
              onChanged: _onChanged,
              decoration: InputDecoration(
                hintText: _hint,
                prefixIcon: const Icon(Icons.search),
                suffixIcon: _controller.text.isEmpty
                    ? null
                    : IconButton(
                        tooltip: 'Wyczyść',
                        icon: const Icon(Icons.clear),
                        onPressed: () {
                          _controller.clear();
                          _load();
                        },
                      ),
              ),
            ),
            const SizedBox(height: 8),
            SegmentedButton<ContactSearchMode>(
              key: const Key('chat-search-mode'),
              showSelectedIcon: false,
              segments: [
                for (final m in ContactSearchMode.values) ButtonSegment(value: m, label: Text(m.label)),
              ],
              selected: {_mode},
              onSelectionChanged: (v) {
                setState(() => _mode = v.first);
                _load();
              },
            ),
            const SizedBox(height: 8),
            Expanded(
              child: _loading
                  ? const Center(child: CircularProgressIndicator())
                  : _error != null
                      ? Center(child: Text(_error!, textAlign: TextAlign.center))
                      : _contacts.isEmpty
                          ? Center(
                              child: Text(_controller.text.trim().isEmpty
                                  ? 'Brak osób, z którymi możesz teraz rozmawiać.'
                                  : 'Brak wyników dla tej frazy.'))
                          : ListView(
                              key: const Key('chat-contacts'),
                              children: [
                                for (final c in _contacts)
                                  ListTile(
                                    leading: CircleAvatar(
                                        child: Icon(switch (c.role) {
                                      UserRole.companyUser => Icons.business,
                                      UserRole.managerUser => Icons.badge_outlined,
                                      UserRole.clientUser => Icons.person,
                                    })),
                                    title: Text(c.name),
                                    subtitle: Text([
                                      c.role.label,
                                      if (c.description != null) c.description!,
                                      if (c.email != null) c.email!,
                                    ].join(' · ')),
                                    onTap: () => Navigator.pop(context, c),
                                  ),
                              ],
                            ),
            ),
          ],
        ),
      ),
      actions: [TextButton(onPressed: () => Navigator.pop(context), child: const Text('Anuluj'))],
    );
  }
}

class _GroupResult {
  const _GroupResult(this.name, this.managerIds);
  final String name;
  final List<String> managerIds;
}

class _GroupDialog extends StatefulWidget {
  const _GroupDialog({required this.managers});

  final List<ChatContact> managers;

  @override
  State<_GroupDialog> createState() => _GroupDialogState();
}

class _GroupDialogState extends State<_GroupDialog> {
  final _name = TextEditingController();
  final _selected = <String>{};

  @override
  void dispose() {
    _name.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final valid = _name.text.trim().isNotEmpty && _selected.isNotEmpty;
    return AlertDialog(
      title: const Text('Nowa grupa firmy'),
      content: SizedBox(
        width: 420,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            TextField(
              key: const Key('chat-group-name'),
              controller: _name,
              maxLength: 100,
              decoration: const InputDecoration(labelText: 'Nazwa grupy'),
              onChanged: (_) => setState(() {}),
            ),
            Flexible(
              child: ListView(
                shrinkWrap: true,
                children: [
                  for (final m in widget.managers)
                    CheckboxListTile(
                      value: _selected.contains(m.userId),
                      title: Text(m.name),
                      onChanged: (v) => setState(() {
                        if (v == true) {
                          _selected.add(m.userId);
                        } else {
                          _selected.remove(m.userId);
                        }
                      }),
                    ),
                ],
              ),
            ),
          ],
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.pop(context), child: const Text('Anuluj')),
        FilledButton(
          onPressed: valid ? () => Navigator.pop(context, _GroupResult(_name.text.trim(), _selected.toList())) : null,
          child: const Text('Utwórz'),
        ),
      ],
    );
  }
}

class _ChatManagerResult {
  const _ChatManagerResult(this.managerId);
  final String? managerId;
}

class _ChatManagerDialog extends StatefulWidget {
  const _ChatManagerDialog({required this.managers, required this.currentId});

  final List<dynamic> managers; // ManagerUser
  final String? currentId;

  @override
  State<_ChatManagerDialog> createState() => _ChatManagerDialogState();
}

class _ChatManagerDialogState extends State<_ChatManagerDialog> {
  String? _selected;

  @override
  void initState() {
    super.initState();
    _selected = widget.currentId;
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: const Text('Manager ds. czatu'),
      content: SizedBox(
        width: 420,
        child: ListView(
          shrinkWrap: true,
          children: [
            const Padding(
              padding: EdgeInsets.only(bottom: 8),
              child: Text('Wskazany manager może rozmawiać ze wszystkimi klientami, także tymi, '
                  'którzy nie współpracują z firmą. Pozostali managerowie - tylko ze swoimi klientami.'),
            ),
            RadioListTile<String?>(
              value: null,
              groupValue: _selected,
              title: const Text('Brak'),
              onChanged: (v) => setState(() => _selected = v),
            ),
            for (final m in widget.managers)
              RadioListTile<String?>(
                value: m.id as String,
                groupValue: _selected,
                title: Text(m.fullName as String),
                onChanged: (v) => setState(() => _selected = v),
              ),
          ],
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.pop(context), child: const Text('Anuluj')),
        FilledButton(
          onPressed: () => Navigator.pop(context, _ChatManagerResult(_selected)),
          child: const Text('Zapisz'),
        ),
      ],
    );
  }
}

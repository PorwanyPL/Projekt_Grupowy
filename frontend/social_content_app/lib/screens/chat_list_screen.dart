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
    try {
      final contacts = await chat.contacts();
      if (!context.mounted) return;
      if (contacts.isEmpty) {
        messenger.show('Brak osób, z którymi możesz teraz rozmawiać.');
        return;
      }
      final picked = await showDialog<ChatContact>(
        context: context,
        builder: (_) => _ContactPickerDialog(contacts: contacts),
      );
      if (picked == null || !context.mounted) return;
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

class _ContactPickerDialog extends StatelessWidget {
  const _ContactPickerDialog({required this.contacts});

  final List<ChatContact> contacts;

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: const Text('Z kim chcesz porozmawiać?'),
      content: SizedBox(
        width: 420,
        child: ListView(
          shrinkWrap: true,
          children: [
            for (final c in contacts)
              ListTile(
                leading: CircleAvatar(
                    child: Icon(switch (c.role) {
                  UserRole.companyUser => Icons.business,
                  UserRole.managerUser => Icons.badge_outlined,
                  UserRole.clientUser => Icons.person,
                })),
                title: Text(c.name),
                subtitle: Text([c.role.label, if (c.description != null) c.description!].join(' · ')),
                onTap: () => Navigator.pop(context, c),
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

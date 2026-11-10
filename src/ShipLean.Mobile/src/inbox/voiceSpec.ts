import type { InboxDto } from '@shiplean/core/api/inbox';
import type { ScreenVoiceSpec } from '../voice/contracts';
import type { InboxAction } from './actions';

// TODO: the Inbox voice actions, grammar and resolution replace this stub.
export function inboxVoiceSpec(inbox: InboxDto): ScreenVoiceSpec<InboxAction> {
  void inbox;
  return {
    screen: 'inbox',
    actions: [],
    context: () => ({}),
    hotwords: () => [],
    toActions: () => ({
      ok: false,
      failure: { kind: 'noValidAction', detail: { action: null, reason: 'none' } },
    }),
  };
}

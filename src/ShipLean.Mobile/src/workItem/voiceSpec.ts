import type { ItemAsksDto } from '@shiplean/core/api/asks';
import type { MemberDto, StateDto } from '@shiplean/core/api/projects';
import type { WorkItemDto } from '@shiplean/core/api/workItems';
import type { ScreenVoiceSpec } from '../voice/contracts';
import type { WorkItemAction } from './actions';

export interface WorkItemVoiceContext {
  item: WorkItemDto;
  asks: ItemAsksDto | null;
  states: StateDto[];
  members: MemberDto[];
}

// TODO: the Work item voice actions and their resolution replace this stub.
export function workItemVoiceSpec(ctx: WorkItemVoiceContext): ScreenVoiceSpec<WorkItemAction> {
  return {
    screen: 'workItem',
    actions: [],
    context: () => ({ key: ctx.item.key }),
    hotwords: () => [],
    toActions: () => ({
      ok: false,
      failure: { kind: 'noValidAction', detail: { action: null, reason: 'none' } },
    }),
  };
}

import type { AskHistoryLineDto, DecisionDto, ItemAsksDto } from '../../api/asks';
import type { TypeRef } from '../../api/refs';

// The pinned props of the section's regions. The section fetches the asks once (useItemAsks) and hands them down: no
// region fetches. Writes go through useAskWrites(item.projectId, item.key, item.id).
//
// DecisionChange and DecisionDiscuss each render one root element with data-testid asksTestIds.decisionChange or
// asksTestIds.decisionDiscuss, and their Cancel calls onClose. The real components keep those root ids.

/** The work item the section sits on. `type` is WorkItemDto.type, passed through. */
export interface AsksItem {
  id: string;
  projectId: string;
  key: string;
  type: TypeRef;
}

/** AsksHeader, QuestionList, DecisionList, BulkConfirm and WaitingChildren. */
export interface AsksRegionProps {
  item: AsksItem;
  asks: ItemAsksDto;
}

/** DecisionChange and DecisionDiscuss: one decision's inline form. */
export interface DecisionEditorProps extends AsksRegionProps {
  decision: DecisionDto;
  onClose(): void;
}

/** One ask line in the History rail. */
export interface AskHistoryLineProps {
  line: AskHistoryLineDto;
}

export type AsksState = 'waiting' | 'settled' | 'empty';

/**
 * waiting: an open question or decision here, or a child Story or Bug waiting (a Feature's roll-up);
 * empty: no questions, no decisions and no waiting children; settled: everything else.
 */
export function asksState(asks: ItemAsksDto): AsksState {
  const open =
    asks.questions.some((question) => question.state === 'open') ||
    asks.decisions.some((decision) => decision.state === 'open');
  if (open || asks.waitingChildren.length > 0) return 'waiting';
  if (asks.questions.length === 0 && asks.decisions.length === 0) return 'empty';
  return 'settled';
}

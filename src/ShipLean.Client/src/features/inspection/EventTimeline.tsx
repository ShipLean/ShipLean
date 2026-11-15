import type { RunAxisDto, RunTurnDto } from '@shiplean/core/api/inspection';
import { inspectionTestIds as ids } from './testIds';

export interface EventTimelineProps {
  turns: RunTurnDto[];
  axis: RunAxisDto;
  selectedRowId: string | null;
  onSelect: (rowId: string | null) => void;
}

// The log-only run's flat event rows by turn (D19).
// TODO: the event timeline replaces this stub.
export function EventTimeline({ turns, selectedRowId }: EventTimelineProps) {
  return (
    <div
      data-testid={ids.runEvents}
      data-turns={turns.length}
      data-selected={selectedRowId ?? ''}
    />
  );
}

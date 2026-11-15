import type { RunAxisDto, RunTurnDto } from '@shiplean/core/api/inspection';
import { inspectionTestIds as ids } from './testIds';

export interface WaterfallProps {
  turns: RunTurnDto[];
  axis: RunAxisDto;
  selectedRowId: string | null;
  onSelect: (rowId: string | null) => void;
  contentShown: boolean;
}

// The traced run's rows by turn on one time axis (D5, D18).
// TODO: the waterfall replaces this stub.
export function Waterfall({ turns, selectedRowId }: WaterfallProps) {
  return (
    <div
      data-testid={ids.runWaterfall}
      data-turns={turns.length}
      data-selected={selectedRowId ?? ''}
    />
  );
}

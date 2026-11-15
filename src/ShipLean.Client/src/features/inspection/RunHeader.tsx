import type { RunHeaderDto } from '@shiplean/core/api/inspection';
import { inspectionTestIds as ids } from './testIds';

export interface RunHeaderProps {
  project: string;
  itemKey: string;
  header: RunHeaderDto;
  errorsOnly: boolean;
  onErrorsOnly: (on: boolean) => void;
  query: string;
  onQuery: (text: string) => void;
}

// The run page's header: agent, start, who, link source, Older and Newer, totals, All or Errors only, find a span.
// TODO: the real header replaces this stub.
export function RunHeader({ header }: RunHeaderProps) {
  return <header data-testid={ids.runHeader}>{header.agentLabel}</header>;
}

import type { ImportJobState } from '../../../api/persisted';
import { Chip } from '../../../components/Chip';
import { importTestIds as ids } from '../testIds';
import { jobStates } from './jobStates';

export function JobChip({ status }: { status: ImportJobState }) {
  const { tone, label } = jobStates[status];
  return (
    <span data-testid={ids.runChip} data-status={status}>
      <Chip tone={tone}>{label}</Chip>
    </span>
  );
}

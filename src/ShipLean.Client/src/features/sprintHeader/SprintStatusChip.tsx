import type { SprintDto } from '../../api/sprints';
import { Chip } from '../../components/Chip';

export function SprintStatusChip({ status }: { status: SprintDto['status'] }) {
  if (status === 'current') return <Chip tone="accent">Current</Chip>;
  if (status === 'past') return <Chip>Ended</Chip>;
  return null;
}

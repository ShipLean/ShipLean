import { Chip } from '../../../components/Chip';
import { backlogTestIds as ids } from '../testIds';

/** "N in triage": the items a GitHub issue opened that wait for a person, which the backlog leaves out. Click filters to them. */
export function TriageChip({ count, onShow }: { count: number; onShow: () => void }) {
  if (count <= 0) return null;
  return (
    <button
      type="button"
      data-testid={ids.triageChip}
      title="Show the items in triage"
      onClick={onShow}
    >
      <Chip tone="info">{count} in triage</Chip>
    </button>
  );
}

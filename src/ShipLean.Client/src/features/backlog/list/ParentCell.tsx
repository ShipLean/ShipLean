import { TypeIcon } from '../../../components/TypeIcon';
import type { ParentRefDto } from '../../../api/workItems';
import { backlogTestIds as ids } from '../testIds';

export function ParentCell({ parent }: { parent: ParentRefDto | null }) {
  if (parent === null) return <span className="text-ink-muted">–</span>;
  return (
    <span data-testid={ids.parent} className="inline-flex items-center gap-1.5">
      <TypeIcon type={parent.type.id} />
      {parent.title}
    </span>
  );
}

import { StateDot } from '../../../../components/StateDot';
import { TypeIcon } from '../../../../components/TypeIcon';
import type { ChildSummaryDto } from '../../../../api/workItems';
import { boardTestIds as ids } from '../../testIds';

// One child still open: its type, title and state.
export function OpenChildRow({ child }: { child: ChildSummaryDto }) {
  return (
    <li
      data-testid={ids.doneConfirmChild}
      data-key={child.key}
      title={`${child.key} ${child.title} · ${child.state.name}`}
      className="flex min-w-0 items-center gap-2 text-[length:var(--text-sm)]"
    >
      <TypeIcon type={child.type.id} />
      <span className="min-w-0 flex-1 truncate text-ink">{child.title}</span>
      <StateDot category={child.state.category} name={child.state.name} />
    </li>
  );
}

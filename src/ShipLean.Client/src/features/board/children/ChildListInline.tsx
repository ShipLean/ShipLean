import { boardTestIds as ids } from '../testIds';
import type { OpenChildList } from './ChildList';
import { ChildListBody } from './ChildListBody';
import type { ChildListHost } from './ChildListContext';

// At touch widths the list opens inside its card, under a divider, as the design draws it: no floating panel to cover
// other cards. The roll-up that opened it closes it again, as does Escape.
export function ChildListInline({ open, host }: { open: OpenChildList; host: ChildListHost }) {
  return (
    <div
      role="region"
      aria-label={`Children of ${open.itemKey}`}
      data-testid={ids.childList}
      data-inline={true}
      data-item={open.itemKey}
      data-type={open.type}
      className="-mx-2 grid gap-1 border-t border-edge px-2 pt-1"
      onKeyDown={(event) => {
        if (event.key === 'Escape') host.close(true);
      }}
    >
      <ChildListBody
        projectId={host.projectId}
        projectKey={host.projectKey}
        boardId={host.boardId}
        level={host.level}
        itemId={open.itemId}
        type={open.type}
      />
    </div>
  );
}

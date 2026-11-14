import { TypeIcon } from '../../../../components/TypeIcon';
import { ItemLink } from '../../../itemPeek/ItemLink';
import { Sheet } from '../../../../touch/Sheet';
import { sprintsTestIds as ids } from '../../testIds';
import type { ReadyItem } from './railSummary';

/** What nobody has started and nobody owns, in backlog order, in a bottom sheet. Tapping an item closes the sheet and opens it. */
export function ReadySheet({
  open,
  ready,
  onClose,
}: {
  open: boolean;
  ready: ReadyItem[];
  onClose: () => void;
}) {
  return (
    <Sheet open={open} title={`Ready to pick up · ${ready.length}`} onClose={onClose}>
      <p className="m-0 px-1 pb-1 text-[length:var(--text-sm)] text-ink-muted">
        Unassigned and not started, in backlog order.
      </p>
      {ready.length === 0 ? (
        <p
          data-testid={ids.railReadyEmpty}
          className="m-0 px-1 py-2 text-[length:var(--text-sm)] text-ink-muted"
        >
          Nothing waiting.
        </p>
      ) : (
        ready.map(({ item, sub }) => (
          <ItemLink
            key={item.id}
            itemKey={item.key}
            testId={ids.glanceReadyItem}
            onClick={onClose}
            className="flex min-h-12 items-start gap-2 rounded-control px-1 py-2 text-inherit no-underline active:bg-signal-soft"
          >
            <TypeIcon type={item.type.id} />
            <span className="grid min-w-0">
              <span className="text-[length:var(--text-base)]">{item.title}</span>
              <small className="truncate text-ink-muted">{sub}</small>
            </span>
          </ItemLink>
        ))
      )}
    </Sheet>
  );
}

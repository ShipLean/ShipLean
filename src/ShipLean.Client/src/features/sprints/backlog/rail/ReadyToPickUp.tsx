import { TypeIcon } from '../../../../components/TypeIcon';
import { ItemLink } from '../../../itemPeek/ItemLink';
import { sprintsTestIds as ids } from '../../testIds';
import type { ReadyItem } from './railSummary';

/** What nobody has started and nobody owns, in backlog order: the next things to pick up. */
export function ReadyToPickUp({ ready }: { ready: ReadyItem[] }) {
  return (
    <section data-testid={ids.railReady} className="grid gap-1.5">
      <div className="text-[length:var(--text-xs)] font-semibold tracking-wide text-ink-muted uppercase">
        Ready to pick up
      </div>
      <p className="m-0 text-[length:var(--text-xs)] text-ink-muted">
        Unassigned and not started, in backlog order.
      </p>
      {ready.length === 0 ? (
        <span
          data-testid={ids.railReadyEmpty}
          className="text-[length:var(--text-sm)] text-ink-muted"
        >
          Nothing waiting.
        </span>
      ) : (
        ready.map(({ item, sub }) => (
          <div
            key={item.id}
            data-testid={ids.railReadyItem}
            data-key={item.key}
            className="flex items-start gap-2 text-[length:var(--text-sm)]"
          >
            <TypeIcon type={item.type.id} />
            <span className="grid min-w-0">
              <ItemLink itemKey={item.key} className="text-inherit no-underline hover:underline">
                {item.title}
              </ItemLink>
              <small className="truncate text-ink-muted">{sub}</small>
            </span>
          </div>
        ))
      )}
    </section>
  );
}

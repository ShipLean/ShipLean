import { Link } from 'react-router-dom';
import type { RetroItemDto } from '@shiplean/core/api/retros';
import { TypeIcon } from '../../../components/TypeIcon';
import { routePath } from '../../../routes';
import { useTouchShell } from '../../../touch/useTouchShell';
import { retrosTestIds as ids } from '../testIds';
import { ItemKpiButtons } from './touch/ItemKpiButtons';

const plural = (count: number, noun: string) => `${count} ${noun}${count === 1 ? '' : 's'}`;

function Kpi({ value, of, label }: { value: number; of?: number; label: string }) {
  return (
    <div>
      <div className="text-[20px] leading-tight font-bold tabular-nums">
        {value}
        {of === undefined ? null : (
          <small className="text-[length:var(--text-sm)] font-medium text-ink-muted"> / {of}</small>
        )}
      </div>
      <div className="text-[length:var(--text-xs)] text-ink-muted">{label}</div>
    </div>
  );
}

/** The item's title row (type, key, title, Open work item), what it holds, its Part-of line and its KPIs. */
export function ItemHeader({
  projectKey,
  item,
  toDecideOn,
  onToDecide,
  onActions,
}: {
  projectKey: string;
  item: RetroItemDto;
  toDecideOn: boolean;
  onToDecide: () => void;
  onActions: () => void;
}) {
  const { touch } = useTouchShell();
  const isFeature = item.item.type.id === 'feature';
  const childRetros = item.children.reduce((sum, child) => sum + child.retros, 0);
  const summary = isFeature
    ? `${plural(item.cards.length, 'retro')} on the Feature, ${childRetros} on its stories and bugs`
    : plural(item.cards.length, 'retro');
  return (
    <section aria-label="Item" className="flex flex-wrap items-start gap-x-6 gap-y-3">
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-2">
          <TypeIcon type={item.item.type.id} />
          <span className="font-mono text-[length:var(--text-sm)] text-ink-muted">
            {item.item.key}
          </span>
          <h1 className="text-[length:var(--text-lg)] font-semibold touch:min-w-0 touch:[overflow-wrap:anywhere]">
            {item.item.title}
          </h1>
          <Link
            data-testid={ids.retroItemOpenWorkItem}
            className="rounded-control border border-edge bg-panel px-3 py-1 text-[length:var(--text-sm)] text-ink touch:inline-flex touch:min-h-[var(--touch-target)] touch:items-center"
            to={routePath('workItem', { key: projectKey, itemKey: item.item.key })}
          >
            Open work item
          </Link>
        </div>
        {item.parent ? (
          <p className="mt-1 text-[length:var(--text-sm)] text-ink-muted touch:[overflow-wrap:anywhere]">
            Part of{' '}
            <Link
              data-testid={ids.retroItemPartOf}
              className="text-signal underline touch:inline-flex touch:min-h-[var(--touch-target)] touch:items-center"
              to={routePath('retroItem', { key: projectKey, itemKey: item.parent.key })}
            >
              {item.parent.key} {item.parent.title}
            </Link>
          </p>
        ) : null}
        <p className="mt-1 text-[length:var(--text-sm)] text-ink-muted">{summary}</p>
      </div>
      {touch ? (
        <div className="w-full">
          <ItemKpiButtons
            totals={item.totals}
            toDecideOn={toDecideOn}
            onToDecide={onToDecide}
            onActions={onActions}
          />
        </div>
      ) : (
        <div className="flex gap-6">
          <Kpi value={item.totals.toDecide} label="To decide" />
          <Kpi value={item.totals.actions} label="Action items" />
          <Kpi value={item.totals.decided} of={item.totals.decidable} label="Decided" />
        </div>
      )}
    </section>
  );
}

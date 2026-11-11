import type { AsksRegionProps } from './regions';
import { DecisionRow } from './DecisionRow';

// The Decisions group: every decision in number order. An open one stands as recommended until someone answers it.
export function DecisionList({ item, asks }: AsksRegionProps) {
  if (asks.decisions.length === 0) return null;
  return (
    <div className="grid gap-1">
      <div className="flex items-baseline gap-2 text-[length:var(--text-xs)] text-ink-muted">
        <span className="font-semibold tracking-wide uppercase">Decisions</span>
        <span>{asks.decisions.length}</span>
        <span>· in number order. An open one stands as recommended until someone answers it.</span>
      </div>
      <ul className="m-0 grid list-none p-0">
        {asks.decisions.map((decision) => (
          <DecisionRow key={decision.id} item={item} asks={asks} decision={decision} />
        ))}
      </ul>
    </div>
  );
}

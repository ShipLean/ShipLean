import { useState } from 'react';
import { AsksHeader } from './AsksHeader';
import { DecisionList } from './DecisionList';
import { QuestionList } from './QuestionList';
import { asksState, type AsksItem } from './regions';
import { asksTestIds as ids } from './testIds';
import { useAskEvents } from './useAskEvents';
import { useItemAsks } from './useItemAsks';
import { WaitingChildren } from './WaitingChildren';

// "Decisions and questions" (D9, UI1): first in a Story's main column, after Goal on a Feature. The section fetches the
// asks once and hands them to its regions. It owns the heading and the fold; AsksHeader renders the rest of the header
// row. Nothing waiting folds to one line (UI8); the fold is not remembered.
export function DecisionsSection({ item }: { item: AsksItem }) {
  const asks = useItemAsks(item.projectId, item.key, item.id);
  useAskEvents(item.projectId, item.key, item.id);
  const [chosen, setChosen] = useState<boolean | null>(null);
  const state = asks.data ? asksState(asks.data) : null;
  const folded = chosen ?? state === 'settled';
  const waiting = state === 'waiting';
  return (
    <section
      data-testid={ids.section}
      aria-label="Decisions and questions"
      className={`grid min-w-0 gap-2 border-b border-l-[3px] border-b-edge/60 px-4 py-3 touch:px-3 ${
        waiting
          ? 'border-l-caution bg-[color-mix(in_srgb,var(--color-warn)_5%,var(--color-surface))]'
          : folded
            ? 'border-l-state-completed'
            : 'border-l-transparent'
      } ${folded ? 'py-2' : ''}`}
    >
      <header data-testid={ids.header} className="flex min-h-6 flex-wrap items-center gap-2">
        {state && state !== 'empty' ? (
          <button
            type="button"
            data-testid={ids.foldToggle}
            aria-expanded={!folded}
            aria-label={`${folded ? 'Show' : 'Fold'} decisions and questions`}
            className="size-[22px] rounded-control text-[length:var(--text-xs)] text-ink-muted hover:bg-signal-soft touch:size-8"
            onClick={() => setChosen(!folded)}
          >
            {folded ? '▸' : '▾'}
          </button>
        ) : null}
        <h2 className="text-[length:var(--text-md)] font-semibold">Decisions and questions</h2>
        {asks.data ? <AsksHeader item={item} asks={asks.data} folded={folded} /> : null}
      </header>
      {asks.isError ? (
        <p
          data-testid={ids.loadFailed}
          role="alert"
          className="m-0 text-[length:var(--text-sm)] text-alert"
        >
          The decisions and questions could not be loaded.
        </p>
      ) : asks.data ? (
        state === 'empty' ? (
          <p data-testid={ids.empty} className="m-0 text-[length:var(--text-xs)] text-ink-muted">
            When an agent records a decision with <code>record_decisions</code> or asks with{' '}
            <code>ask</code>, it shows here for{' '}
            {asks.data.ownerIsMe ? 'you' : (asks.data.owner?.name ?? 'the owner')} to answer.
          </p>
        ) : folded ? null : (
          <div className="grid max-w-[940px] min-w-0 gap-3">
            <QuestionList item={item} asks={asks.data} />
            <DecisionList item={item} asks={asks.data} />
            {item.type.id === 'feature' ? <WaitingChildren item={item} asks={asks.data} /> : null}
          </div>
        )
      ) : (
        <p data-testid={ids.loading} className="m-0 text-[length:var(--text-sm)] text-ink-muted">
          Loading…
        </p>
      )}
    </section>
  );
}

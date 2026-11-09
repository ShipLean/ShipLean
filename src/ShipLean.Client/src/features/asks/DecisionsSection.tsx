import { AsksHeader } from './AsksHeader';
import { DecisionList } from './DecisionList';
import { QuestionList } from './QuestionList';
import type { AsksItem } from './regions';
import { asksTestIds as ids } from './testIds';
import { useAskEvents } from './useAskEvents';
import { useItemAsks } from './useItemAsks';
import { WaitingChildren } from './WaitingChildren';

// "Decisions and questions" (D9, UI1): first in a Story's main column, after Goal on a Feature. The section fetches the
// asks once and hands them to its regions. It owns the heading; AsksHeader renders the rest of the header row.
export function DecisionsSection({ item }: { item: AsksItem }) {
  const asks = useItemAsks(item.projectId, item.key, item.id);
  useAskEvents(item.projectId, item.key, item.id);
  return (
    <section
      data-testid={ids.section}
      aria-label="Decisions and questions"
      className="grid max-w-[780px] gap-3"
    >
      <header data-testid={ids.header} className="flex items-center gap-2">
        <h2 className="text-[length:var(--text-md)] font-semibold">Decisions and questions</h2>
        {asks.data ? <AsksHeader item={item} asks={asks.data} /> : null}
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
        <>
          <QuestionList item={item} asks={asks.data} />
          <DecisionList item={item} asks={asks.data} />
          {item.type.id === 'feature' ? <WaitingChildren item={item} asks={asks.data} /> : null}
        </>
      ) : (
        <p data-testid={ids.loading} className="m-0 text-[length:var(--text-sm)] text-ink-muted">
          Loading…
        </p>
      )}
    </section>
  );
}

import { Avatar } from '../../components/Avatar';
import { Chip } from '../../components/Chip';
import type { ItemAsksDto } from '../../api/asks';
import { BulkConfirm } from './BulkConfirm';
import type { AsksRegionProps } from './regions';
import { asksState } from './regions';
import { asksTestIds as ids } from './testIds';

const plural = (n: number, one: string, many = `${one}s`) => `${n} ${n === 1 ? one : many}`;

const isOpen = (ask: { state: string }) => ask.state === 'open';

// "6 decisions: 4 confirmed, 1 changed, 1 withdrawn · 2 questions answered" (the design's summary()).
function settledSummary(asks: ItemAsksDto): string {
  const parts: string[] = [];
  if (asks.decisions.length > 0) {
    const counted = (
      [
        ['confirmed', 'confirmed'],
        ['changed', 'changed'],
        ['open', 'going with recommended'],
        ['withdrawn', 'withdrawn'],
      ] as const
    )
      .map(
        ([state, label]) =>
          [asks.decisions.filter((d) => d.state === state).length, label] as const,
      )
      .filter(([count]) => count > 0)
      .map(([count, label]) => `${count} ${label}`);
    parts.push(`${plural(asks.decisions.length, 'decision')}: ${counted.join(', ')}`);
  }
  if (asks.questions.length > 0) {
    const answered = asks.questions.filter((q) => q.state === 'answered').length;
    parts.push(
      answered === asks.questions.length
        ? `${plural(answered, 'question')} answered`
        : `${plural(asks.questions.length, 'question')}, ${asks.questions.filter(isOpen).length} open`,
    );
  }
  return parts.join(' · ');
}

// The rest of the section's header row, after the fold button and the heading (DecisionsSection owns those): the count,
// who it waits on, and the one-line summary.
export function AsksHeader({ item, asks, folded = false }: AsksRegionProps & { folded?: boolean }) {
  const openQuestions = asks.questions.filter(isOpen).length;
  const openDecisions = asks.decisions.filter(isOpen).length;
  const waiting = openQuestions + openDecisions;
  const state = asksState(asks);
  if (state === 'empty')
    return (
      <span data-testid={ids.summary} className="text-[length:var(--text-sm)] text-ink-muted">
        No decisions or questions yet.
      </span>
    );
  const ownerWhy = asks.ownerIsCreator ? 'creator; nobody is assigned' : 'assignee';
  const openBits = [
    openQuestions > 0 && plural(openQuestions, 'question'),
    openDecisions > 0 && `${plural(openDecisions, 'decision')} going with recommended`,
  ]
    .filter(Boolean)
    .join(' · ');
  const childAsks = asks.waitingChildren.reduce(
    (sum, child) => sum + child.openQuestions + child.openDecisions,
    0,
  );
  return (
    <>
      {waiting > 0 ? (
        <>
          <span
            data-testid={ids.count}
            title="Waiting"
            className="text-[length:var(--text-xs)] text-ink-muted tabular-nums"
          >
            {waiting}
          </span>
          {asks.owner ? (
            <span
              data-testid={ids.waitingOn}
              title={`The item's owner: its ${ownerWhy}. Anyone who can edit ${item.key} can answer.`}
            >
              <Chip tone="warn">
                <Avatar {...asks.owner} />
                Waiting on {asks.ownerIsMe ? 'you' : asks.owner.name}
              </Chip>
            </span>
          ) : null}
        </>
      ) : null}
      <span
        data-testid={ids.summary}
        className="min-w-0 text-[length:var(--text-sm)] text-ink-muted"
      >
        {waiting > 0 ? (
          <>
            <b className="font-medium text-ink">{openBits}</b>
            {asks.ownerIsMe ? '' : ' · you can answer too'}
          </>
        ) : childAsks > 0 ? (
          <>
            <b className="font-medium text-ink">Nothing here waits.</b> {plural(childAsks, 'ask')}{' '}
            on {plural(asks.waitingChildren.length, 'story', 'stories')} below
          </>
        ) : (
          <>
            <Chip tone="ok">✓ Nothing waiting</Chip> {settledSummary(asks)}
          </>
        )}
      </span>
      <span className="ml-auto" />
      {folded ? null : <BulkConfirm item={item} asks={asks} />}
    </>
  );
}

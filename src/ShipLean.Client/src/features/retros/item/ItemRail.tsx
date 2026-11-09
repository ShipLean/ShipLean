import { Link } from 'react-router-dom';
import type { RetroItemDto } from '@shiplean/core/api/retros';
import { Chip } from '../../../components/Chip';
import { StateDot } from '../../../components/StateDot';
import { TypeIcon } from '../../../components/TypeIcon';
import { routePath } from '../../../routes';
import { retroFixOptions } from '../../retroShared/actionDialog/retroFixTypes';
import { AuthorChip } from '../../retroShared/AuthorChip';
import { ItemLink } from '../../itemPeek/ItemLink';
import { retrosTestIds as ids } from '../testIds';

const roleLabels = { conductor: 'Conductor', subAgent: 'Sub-agent', person: 'Person' } as const;

function Overline({ children }: { children: string }) {
  return (
    <div className="text-[length:var(--text-2xs)] font-semibold tracking-wide text-ink-muted uppercase">
      {children}
    </div>
  );
}

/** The item's rail: its action items (a Feature's include its stories' and bugs'), what a Feature's retro was compiled from, and who wrote each retro. */
export function ItemRail({ projectKey, item }: { projectKey: string; item: RetroItemDto }) {
  const compiled = [
    ...new Map(
      item.cards.flatMap((card) => card.compiledFrom).map((ref) => [ref.id, ref]),
    ).values(),
  ];
  return (
    <aside
      data-testid={ids.retroItemRail}
      className="grid min-w-0 grid-cols-[minmax(0,1fr)] content-start gap-5 border-l border-edge pl-4 text-[length:var(--text-sm)]"
    >
      <section
        data-testid={ids.retroItemRailActions}
        className="grid min-w-0 grid-cols-[minmax(0,1fr)] gap-1.5"
      >
        <Overline>Action items</Overline>
        <p className="m-0 text-[length:var(--text-xs)] text-ink-muted">
          Raised from these points. Each starts in New with no sprint; pulling it into a sprint is
          the approval.
        </p>
        {item.actions.length === 0 ? (
          <span className="text-[length:var(--text-xs)] text-ink-muted">None yet.</span>
        ) : (
          item.actions.map((action) => {
            const fix = retroFixOptions.find((option) => option.id === action.fixType);
            return (
              <div
                key={action.workItemId}
                data-item-key={action.key}
                className="grid min-w-0 grid-cols-[minmax(0,1fr)] gap-0.5 border-b border-edge/60 pb-1.5"
              >
                <div className="flex items-center gap-1.5">
                  <TypeIcon type="action" />
                  <span className="min-w-0 truncate" title={action.title}>
                    {action.title}
                  </span>
                </div>
                <div className="flex flex-wrap items-center gap-1.5 text-[length:var(--text-xs)] text-ink-muted">
                  <ItemLink
                    itemKey={action.key}
                    title={action.title}
                    className="font-mono text-ink-muted no-underline"
                  >
                    {action.key}
                  </ItemLink>
                  <StateDot category={action.state.category} name={action.state.name} />
                  <span data-planned="">· {action.sprint?.name ?? 'No sprint'}</span>
                  {fix ? <span>· {fix.label}</span> : null}
                </div>
              </div>
            );
          })
        )}
      </section>
      {compiled.length > 0 ? (
        <section
          data-testid={ids.retroItemRailCompiled}
          className="grid min-w-0 grid-cols-[minmax(0,1fr)] gap-1.5"
        >
          <Overline>Compiled from</Overline>
          {compiled.map((ref) => (
            <div key={ref.id} className="flex min-w-0 items-center gap-1.5">
              <TypeIcon type={ref.type.id} />
              <Link
                className="min-w-0 truncate text-ink no-underline hover:underline"
                to={routePath('retroItem', { key: projectKey, itemKey: ref.key })}
              >
                {ref.key} {ref.title}
              </Link>
            </div>
          ))}
        </section>
      ) : null}
      {item.cards.length > 0 ? (
        <section
          data-testid={ids.retroItemRailWriters}
          className="grid min-w-0 grid-cols-[minmax(0,1fr)] gap-1.5"
        >
          <Overline>Who wrote this</Overline>
          {item.cards.map((card) => (
            <div key={card.id} data-retro-id={card.id} className="grid gap-0.5">
              <div className="flex flex-wrap items-center gap-1.5">
                <AuthorChip author={card.author} />
                <Chip>{roleLabels[card.author.role]}</Chip>
              </div>
              <div className="text-[length:var(--text-xs)] text-ink-muted">
                {card.createdAtLabel} · {card.sprint?.name ?? 'No sprint'}
              </div>
            </div>
          ))}
        </section>
      ) : null}
    </aside>
  );
}

import { useState } from 'react';
import { Chip } from '../../components/Chip';
import { ItemLink } from '../itemPeek/ItemLink';
import { TypeIcon } from '../../components/TypeIcon';
import { RETRO_CATEGORIES } from '../../api/retros';
import type { RetroCardDto, RetroFailureLogDto } from '../../api/retros';
import { AuthorChip } from './AuthorChip';
import { FeatureChip } from './FeatureChip';
import { RetroCardCategory } from './RetroCardCategory';
import { RetroLogSection } from './RetroLogSection';
import { retroSharedTestIds as ids } from './testIds';

const roleLabels = { conductor: 'Conductor', subAgent: 'Sub-agent', person: 'Person' } as const;

const logSections: { key: keyof RetroFailureLogDto; label: string }[] = [
  { key: 'blockers', label: 'Blockers' },
  { key: 'assumptionsDisproved', label: 'Assumptions disproved' },
  { key: 'friction', label: 'Friction' },
];

/** One retro as the By work item view shows it: the item (or the sprint), who wrote it, its points by category, and the raw failure log collapsed. */
export function RetroCard({
  card,
  highlightPointId,
  logDefaultOpen = false,
  hideEmptyCategories = false,
  showActions = true,
}: {
  card: RetroCardDto;
  /** Search's selected retro: this point is highlighted in its category. */
  highlightPointId?: string;
  /** The raw failure log starts open instead of collapsed. */
  logDefaultOpen?: boolean;
  /** Leave out categories with no points instead of showing them with "—". */
  hideEmptyCategories?: boolean;
  /** Show each point's action item chip. The By work item view leaves them out. */
  showActions?: boolean;
}) {
  const [logOpen, setLogOpen] = useState(logDefaultOpen);
  const { workItem, sprint, failureLog } = card;
  const isFeature = workItem?.type.id === 'feature';
  const hasLog = logSections.some((s) => failureLog[s.key].length > 0);
  return (
    <article
      data-testid={ids.retroCard}
      data-feature={isFeature ? 'true' : 'false'}
      className={`@container grid gap-2 rounded-md border border-edge bg-panel p-3 ${isFeature ? 'border-l-[3px] border-l-signal' : ''}`}
    >
      <header className="flex flex-wrap items-center gap-1.5 text-[length:var(--text-sm)]">
        {isFeature ? <FeatureChip /> : null}
        {workItem ? (
          <>
            <TypeIcon type={workItem.type.id} />
            <span className="font-mono">{workItem.key}</span>
            <b className="text-[length:var(--text-md)]">{workItem.title}</b>
          </>
        ) : (
          <>
            <Chip>Sprint</Chip>
            <b className="text-[length:var(--text-md)]">{sprint?.name}</b>
          </>
        )}
        {card.partial ? <Chip tone="warn">Rolled over</Chip> : null}
        <span className="flex-1" />
        <AuthorChip author={card.author} />
        <Chip>{roleLabels[card.author.role]}</Chip>
        <span data-created-at="" className="text-[length:var(--text-xs)] text-ink-muted">
          {card.createdAtLabel}
        </span>
      </header>
      <div className="grid grid-cols-1 gap-3 @min-[480px]:grid-cols-2 @min-[760px]:grid-cols-4">
        {RETRO_CATEGORIES.map((category) => {
          const points = card.points.filter((p) => p.category === category);
          if (hideEmptyCategories && points.length === 0) return null;
          return (
            <RetroCardCategory
              key={category}
              category={category}
              points={points}
              highlightPointId={highlightPointId}
              showActions={showActions}
            />
          );
        })}
      </div>
      {card.compiledFrom.length > 0 ? (
        <p className="m-0 flex flex-wrap items-center gap-1.5 text-[length:var(--text-xs)] text-ink-muted">
          Compiled from
          {card.compiledFrom.map((item, i) => (
            <span key={item.id}>
              <span className="inline-flex items-center gap-1">
                <TypeIcon type={item.type.id} />
                <ItemLink itemKey={item.key} title={item.title} className="font-mono">
                  {item.key}
                </ItemLink>
              </span>
              {i < card.compiledFrom.length - 1 ? ',' : '.'}
            </span>
          ))}
        </p>
      ) : null}
      {hasLog ? (
        <div className="rounded-sm border border-edge bg-canvas">
          <button
            type="button"
            data-testid={ids.retroCardLogToggle}
            aria-expanded={logOpen}
            className="w-full cursor-pointer px-2.5 py-1.5 text-left text-[length:var(--text-sm)] font-semibold"
            onClick={() => setLogOpen(!logOpen)}
          >
            {logOpen ? '▾' : '▸'} Raw failure log{' '}
            <span className="font-normal text-ink-muted">· verbatim from the agent</span>
          </button>
          {logOpen ? (
            <dl
              data-testid={ids.retroCardLog}
              className="m-0 grid grid-cols-[160px_minmax(0,1fr)] gap-x-3 gap-y-1.5 px-2.5 pb-2.5 text-[length:var(--text-sm)]"
            >
              {logSections.map((s) => (
                <RetroLogSection key={s.key} label={s.label} lines={failureLog[s.key]} />
              ))}
            </dl>
          ) : null}
        </div>
      ) : null}
    </article>
  );
}

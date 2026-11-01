import { useState } from 'react';
import { Chip } from '../../components/Chip';
import { TypeIcon } from '../../components/TypeIcon';
import { RETRO_CATEGORIES } from '../../api/retros';
import type { RetroCardDto, RetroFailureLogDto } from '../../api/retros';
import { AuthorChip } from './AuthorChip';
import { RetroLogSection } from './RetroLogSection';
import { retroCategoryColours, retroCategoryLabels } from './retroCategories';
import { retroSharedTestIds as ids } from './testIds';
import { ThemeChip } from './ThemeChip';

const roleLabels = { conductor: 'Conductor', subAgent: 'Sub-agent', person: 'Person' } as const;

const logSections: { key: keyof RetroFailureLogDto; label: string }[] = [
  { key: 'blockers', label: 'Blockers' },
  { key: 'assumptionsDisproved', label: 'Assumptions disproved' },
  { key: 'friction', label: 'Friction' },
];

/** One retro as the By work item view shows it: the item (or the sprint), who wrote it, its points by category, and the raw failure log collapsed. */
export function RetroCard({ card }: { card: RetroCardDto }) {
  const [logOpen, setLogOpen] = useState(false);
  const { workItem, sprint, failureLog } = card;
  const isFeature = workItem?.type.id === 'feature';
  const hasLog = logSections.some((s) => failureLog[s.key].length > 0);
  return (
    <article
      data-testid={ids.retroCard}
      data-feature={isFeature ? 'true' : 'false'}
      className={`grid gap-2 rounded-md border border-edge bg-panel p-3 ${isFeature ? 'border-l-[3px] border-l-signal' : ''}`}
    >
      <header className="flex flex-wrap items-center gap-1.5 text-[length:var(--text-sm)]">
        {isFeature ? <Chip tone="accent">Feature</Chip> : null}
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
      </header>
      <div className="grid grid-cols-2 gap-3 min-[1100px]:grid-cols-4">
        {RETRO_CATEGORIES.map((category) => {
          const points = card.points.filter((p) => p.category === category);
          return (
            <section
              key={category}
              data-category={category}
              aria-label={retroCategoryLabels[category]}
              className="min-w-0 border-l-2 pl-2"
              style={{ borderColor: retroCategoryColours[category] }}
            >
              <div className="text-[length:var(--text-2xs)] font-semibold tracking-wide text-ink-muted uppercase">
                {retroCategoryLabels[category]}
              </div>
              {points.length > 0 ? (
                <ul className="m-0 mt-1 grid list-disc gap-1 pl-4 text-[length:var(--text-sm)]">
                  {points.map((p) => (
                    <li key={p.id}>
                      {p.text}
                      {category === 'wentBadly' && p.rootCause ? (
                        <span className="block text-[length:var(--text-xs)] text-ink-muted">
                          Root cause: {p.rootCause}
                        </span>
                      ) : null}
                      <ThemeChip theme={p.theme} recurring={p.recurring} />
                    </li>
                  ))}
                </ul>
              ) : (
                <span className="text-[length:var(--text-xs)] text-ink-muted">-</span>
              )}
            </section>
          );
        })}
      </div>
      {card.compiledFrom.length > 0 ? (
        <p className="m-0 flex flex-wrap items-center gap-1.5 text-[length:var(--text-xs)] text-ink-muted">
          Compiled from
          {card.compiledFrom.map((item) => (
            <span key={item.id} className="inline-flex items-center gap-1">
              <TypeIcon type={item.type.id} />
              <span className="font-mono">{item.key}</span>
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

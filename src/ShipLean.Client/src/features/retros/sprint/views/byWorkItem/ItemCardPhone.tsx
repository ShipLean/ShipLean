import { useState } from 'react';
import { Link } from 'react-router-dom';
import type { RetroCardDto, RetroPointDto } from '@shiplean/core/api/retros';
import { routePath } from '../../../../../routes';
import { Chip } from '../../../../../components/Chip';
import { TypeIcon } from '../../../../../components/TypeIcon';
import { RetroBoard } from '../../../../retroShared/board/RetroBoard';
import { FeatureChip } from '../../../../retroShared/FeatureChip';
import { retroCategoryColours, retroCategoryLabels } from '../../../../retroShared/retroCategories';
import { retroSharedTestIds as ids } from '../../../../retroShared/testIds';
import { DecidedProgress } from '../../../features/DecidedProgress';
import { retrosTestIds } from '../../../testIds';
import { cardAddTarget, cardColumns, categoryCounts } from './cardColumns';
import { FailureLog } from './FailureLog';

/**
 * One item as a collapsed card at touch (SHIP-727 D4): type, key, title, the four category counts, "n to decide" and
 * progress. A tap opens its notes by category (empty categories hidden), each retro's raw failure log collapsed, and a
 * link to the item's retro page.
 */
export function ItemCardPhone({
  projectKey,
  cards,
  sprintId,
  filter,
  highlightedPointId,
  onAdded,
}: {
  projectKey: string;
  cards: RetroCardDto[];
  sprintId: string;
  filter: (point: RetroPointDto) => boolean;
  highlightedPointId?: string;
  onAdded?: (point: RetroPointDto) => void;
}) {
  const [card] = cards;
  const { workItem, sprint } = card;
  const points = cards.flatMap((each) => each.points);
  const [open, setOpen] = useState(
    highlightedPointId !== undefined && points.some((p) => p.id === highlightedPointId),
  );
  const itemPage = workItem
    ? routePath('retroItem', { key: projectKey, itemKey: workItem.key })
    : null;
  const decidable = points.filter((p) => p.decision !== null);
  const progress = {
    decidable: decidable.length,
    decided: decidable.filter((p) => p.decision !== 'undecided').length,
  };
  const toDecide = points.filter((p) => p.decision === 'undecided').length;
  const isFeature = workItem?.type.id === 'feature';
  const shownColumns = cardColumns(points).filter((column) =>
    column.groups.some((group) => group.points.some(filter)),
  );
  return (
    <article
      data-testid={ids.retroCard}
      data-feature={isFeature ? 'true' : 'false'}
      data-item-key={workItem?.key ?? 'sprint'}
      className={`grid min-w-0 rounded-md border border-edge bg-panel ${isFeature ? 'border-l-[3px] border-l-signal' : ''}`}
    >
      <button
        type="button"
        data-testid={retrosTestIds.retroCardToggle}
        aria-expanded={open}
        className="grid min-h-12 w-full cursor-pointer grid-cols-[minmax(0,1fr)_auto] items-center gap-x-2 gap-y-1.5 border-0 bg-transparent p-3 text-left text-[length:var(--text-sm)] text-ink"
        onClick={() => setOpen(!open)}
      >
        <span
          data-testid={retrosTestIds.retroCardHeader}
          className="flex min-w-0 flex-wrap items-center gap-1.5"
        >
          {isFeature ? <FeatureChip /> : null}
          {workItem ? (
            <>
              <TypeIcon type={workItem.type.id} />
              <span className="font-mono underline">{workItem.key}</span>
              <b className="text-[length:var(--text-md)]">{workItem.title}</b>
            </>
          ) : (
            <>
              <Chip>Sprint</Chip>
              <b className="text-[length:var(--text-md)]">{sprint?.name}</b>
            </>
          )}
          {cards.some((each) => each.partial) ? <Chip tone="warn">Rolled over</Chip> : null}
        </span>
        <span aria-hidden="true" className="text-ink-muted">
          {open ? '⌄' : '›'}
        </span>
        <span className="col-span-2 flex flex-wrap items-center gap-x-3 gap-y-1">
          {categoryCounts(points).map(({ category, count }) => (
            <span
              key={category}
              data-testid={retrosTestIds.retroCardCount}
              data-category={category}
              aria-label={`${retroCategoryLabels[category]} ${count}`}
              className={`inline-flex items-center gap-1 tabular-nums ${count === 0 ? 'opacity-50' : ''}`}
            >
              <i
                className="block size-2.5 rounded-[2px]"
                style={{ background: retroCategoryColours[category] }}
              />
              {count}
            </span>
          ))}
          {toDecide > 0 ? (
            <span
              data-testid={retrosTestIds.retroCardToDecide}
              className="rounded-full bg-caution/20 px-2 py-0.5 text-[length:var(--text-xs)] text-caution"
            >
              {toDecide} to decide
            </span>
          ) : null}
          <span className="flex-1" />
          <span data-testid={retrosTestIds.retroCardProgress}>
            <DecidedProgress totals={progress} />
          </span>
        </span>
      </button>
      {open ? (
        <div
          data-testid={retrosTestIds.retroCardBody}
          className="grid gap-2 border-t border-edge p-3"
        >
          {shownColumns.length > 0 ? (
            <RetroBoard
              columns={shownColumns}
              groupByTheme={false}
              filter={filter}
              note={{ showItem: false, highlightedPointId }}
              add={cardAddTarget(card, sprintId)}
              scope={
                workItem ? { kind: 'item', itemId: workItem.id } : { kind: 'sprint', sprintId }
              }
              onAdded={onAdded}
            />
          ) : null}
          {cards.map((each) => (
            <FailureLog key={each.id} log={each.failureLog} />
          ))}
          {itemPage && workItem ? (
            <Link
              to={itemPage}
              data-testid={retrosTestIds.retroCardRetroLink}
              className="inline-flex min-h-[var(--touch-target)] items-center text-[length:var(--text-sm)] font-medium text-ink underline"
            >
              Open {workItem.key} retro ›
            </Link>
          ) : null}
        </div>
      ) : null}
    </article>
  );
}

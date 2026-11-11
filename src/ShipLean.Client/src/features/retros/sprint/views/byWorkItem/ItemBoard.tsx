import { Link } from 'react-router-dom';
import type { RetroCardDto, RetroPointDto } from '@shiplean/core/api/retros';
import { routePath } from '../../../../../routes';
import { Chip } from '../../../../../components/Chip';
import { TypeIcon } from '../../../../../components/TypeIcon';
import { ItemLink } from '../../../../itemPeek/ItemLink';
import { AuthorChip } from '../../../../retroShared/AuthorChip';
import { RetroBoard } from '../../../../retroShared/board/RetroBoard';
import { FeatureChip } from '../../../../retroShared/FeatureChip';
import { retroSharedTestIds as ids } from '../../../../retroShared/testIds';
import { DecidedProgress } from '../../../features/DecidedProgress';
import { retrosTestIds } from '../../../testIds';
import { cardAddTarget, cardColumns } from './cardColumns';
import { FailureLog } from './FailureLog';

/** One item's board: the four columns with the notes of all its retros, Add, each retro's Compiled from line, and each raw failure log below. */
export function ItemBoard({
  projectKey,
  cards,
  sprintId,
  filter,
  highlightedPointId,
  onAdded,
}: {
  projectKey: string;
  /** Every retro of this item (or of the sprint itself), at least one. */
  cards: RetroCardDto[];
  sprintId: string;
  filter: (point: RetroPointDto) => boolean;
  highlightedPointId?: string;
  onAdded?: (point: RetroPointDto) => void;
}) {
  const [card] = cards;
  const { workItem, sprint } = card;
  const itemPage = workItem
    ? routePath('retroItem', { key: projectKey, itemKey: workItem.key })
    : null;
  // Progress counts every point of the item's retros, not the ones the filter shows, so To decide leaves it alone.
  const points = cards.flatMap((each) => each.points);
  const decidable = points.filter((p) => p.decision !== null);
  const progress = {
    decidable: decidable.length,
    decided: decidable.filter((p) => p.decision !== 'undecided').length,
  };
  const isFeature = workItem?.type.id === 'feature';
  return (
    <article
      data-testid={ids.retroCard}
      data-feature={isFeature ? 'true' : 'false'}
      data-item-key={workItem?.key ?? 'sprint'}
      className={`grid min-w-0 gap-2 rounded-md border border-edge bg-panel p-3 ${isFeature ? 'border-l-[3px] border-l-signal' : ''}`}
    >
      <header
        data-testid={retrosTestIds.retroCardHeader}
        className="flex flex-wrap items-center gap-1.5 text-[length:var(--text-sm)]"
      >
        {isFeature ? <FeatureChip /> : null}
        {workItem ? (
          <>
            <TypeIcon type={workItem.type.id} />
            <Link
              to={itemPage!}
              data-testid={retrosTestIds.retroCardKeyLink}
              className="font-mono text-ink hover:underline"
            >
              {workItem.key}
            </Link>
            <b className="text-[length:var(--text-md)]">{workItem.title}</b>
          </>
        ) : (
          <>
            <Chip>Sprint</Chip>
            <b className="text-[length:var(--text-md)]">{sprint?.name}</b>
          </>
        )}
        {cards.some((each) => each.partial) ? <Chip tone="warn">Rolled over</Chip> : null}
        <span className="flex-1" />
        {cards.map((each) => (
          <span key={each.id} className="inline-flex items-center gap-1.5">
            <AuthorChip author={each.author} />
            <span data-created-at="" className="text-[length:var(--text-xs)] text-ink-muted">
              {each.createdAtLabel}
            </span>
          </span>
        ))}
        <span data-testid={retrosTestIds.retroCardProgress}>
          <DecidedProgress totals={progress} />
        </span>
        {itemPage ? (
          <Link
            to={itemPage}
            data-testid={retrosTestIds.retroCardOpen}
            className="rounded-control border border-edge px-2 py-0.5 text-[length:var(--text-xs)] font-medium whitespace-nowrap hover:bg-sunken"
          >
            Open
          </Link>
        ) : null}
      </header>
      <RetroBoard
        columns={cardColumns(cards.flatMap((each) => each.points))}
        groupByTheme={false}
        filter={filter}
        note={{ showItem: false, highlightedPointId }}
        add={cardAddTarget(card, sprintId)}
        scope={workItem ? { kind: 'item', itemId: workItem.id } : { kind: 'sprint', sprintId }}
        onAdded={onAdded}
      />
      {cards.map((each) => (
        <div key={each.id} className="grid gap-2">
          {each.compiledFrom.length > 0 ? (
            <p className="m-0 flex flex-wrap items-center gap-1.5 text-[length:var(--text-xs)] text-ink-muted">
              Compiled from
              {each.compiledFrom.map((item, i) => (
                <span key={item.id}>
                  <span className="inline-flex items-center gap-1">
                    <TypeIcon type={item.type.id} />
                    <ItemLink itemKey={item.key} title={item.title} className="font-mono">
                      {item.key}
                    </ItemLink>
                  </span>
                  {i < each.compiledFrom.length - 1 ? ',' : '.'}
                </span>
              ))}
            </p>
          ) : null}
          <FailureLog log={each.failureLog} />
        </div>
      ))}
    </article>
  );
}

import type { RetroCardDto, RetroPointDto } from '../../../../../api/retros';
import { Chip } from '../../../../../components/Chip';
import { TypeIcon } from '../../../../../components/TypeIcon';
import { ItemLink } from '../../../../itemPeek/ItemLink';
import { AuthorChip } from '../../../../retroShared/AuthorChip';
import { RetroBoard } from '../../../../retroShared/board/RetroBoard';
import { FeatureChip } from '../../../../retroShared/FeatureChip';
import { retroSharedTestIds as ids } from '../../../../retroShared/testIds';
import { cardAddTarget, cardColumns } from './cardColumns';
import { FailureLog } from './FailureLog';

/** One retro's item as a board: who wrote it, the four columns of its notes with Add, the Compiled from line, and its raw failure log below. */
export function ItemBoard({
  card,
  filter,
  highlightedPointId,
  onAdded,
}: {
  card: RetroCardDto;
  filter: (point: RetroPointDto) => boolean;
  highlightedPointId?: string;
  onAdded?: (point: RetroPointDto) => void;
}) {
  const { workItem, sprint } = card;
  const isFeature = workItem?.type.id === 'feature';
  return (
    <article
      data-testid={ids.retroCard}
      data-feature={isFeature ? 'true' : 'false'}
      data-item-key={workItem?.key ?? 'sprint'}
      className={`grid min-w-0 gap-2 rounded-md border border-edge bg-panel p-3 ${isFeature ? 'border-l-[3px] border-l-signal' : ''}`}
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
        <span data-created-at="" className="text-[length:var(--text-xs)] text-ink-muted">
          {card.createdAtLabel}
        </span>
      </header>
      <RetroBoard
        columns={cardColumns(card.points)}
        groupByTheme={false}
        filter={filter}
        note={{ showItem: false, highlightedPointId }}
        add={cardAddTarget(card)}
        scope={
          workItem
            ? { kind: 'item', itemId: workItem.id }
            : { kind: 'sprint', sprintId: sprint?.id ?? '' }
        }
        onAdded={onAdded}
      />
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
      <FailureLog log={card.failureLog} />
    </article>
  );
}

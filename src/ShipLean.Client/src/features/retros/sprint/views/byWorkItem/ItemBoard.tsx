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

/** One item's board: the four columns with the notes of all its retros, Add, each retro's Compiled from line, and each raw failure log below. */
export function ItemBoard({
  cards,
  sprintId,
  filter,
  highlightedPointId,
  onAdded,
}: {
  /** Every retro of this item (or of the sprint itself), at least one. */
  cards: RetroCardDto[];
  sprintId: string;
  filter: (point: RetroPointDto) => boolean;
  highlightedPointId?: string;
  onAdded?: (point: RetroPointDto) => void;
}) {
  const [card] = cards;
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

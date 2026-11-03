import { Link } from 'react-router-dom';
import { routePath } from '../../../routes';
import { ItemLink } from '../../itemPeek/ItemLink';
import { retroSharedTestIds as shared } from '../../retroShared/testIds';
import { RetroBoard } from '../../retroShared/board/RetroBoard';
import { CompiledFrom } from './parts/CompiledFrom';
import { SelectedHeader } from './parts/SelectedHeader';
import { FailureLog } from '../sprint/views/byWorkItem/FailureLog';
import { cardAddTarget, cardColumns } from '../sprint/views/byWorkItem/cardColumns';
import { retrosTestIds as ids } from '../testIds';
import { highlightedRow } from './parts/matches';
import type { RetroActionScope } from '../../retroShared/actionDialog/RetroActionScope';
import type { AddTarget } from '../../retroShared/board/AddTarget';
import type { RetroCardDto } from '../../../api/retros';
import type { RetrosSearchSlotProps } from './SearchSlotProps';

/** The whole retro of the selected point as a board of the same notes and buttons as everywhere else, the clicked point highlighted, and the raw log open when that point matched only there. */
export function SelectedRetro({ project, search, pointId }: RetrosSearchSlotProps) {
  const selected = search.selected;
  // A retro is of an item or of a sprint (K4); a card with neither cannot exist, so it gets no board.
  const target = selected ? boardTarget(selected) : undefined;
  const card = target ? selected : null;
  const isFeature = card?.workItem?.type.id === 'feature';
  const highlighted = highlightedRow(search, pointId);
  const inLog = highlighted !== undefined && search.logMatchPointIds.includes(highlighted.id);
  return (
    <aside
      data-testid={ids.retrosSelected}
      aria-label="Retrospective"
      className="sticky top-[8.5rem] max-h-[calc(100vh-12rem)] min-w-0 self-start overflow-y-auto"
    >
      {card && target ? (
        <div className="grid gap-2">
          {card.workItem || card.sprint ? (
            <div className="flex flex-wrap gap-2">
              {card.workItem ? (
                <ItemLink
                  projectKey={project.key}
                  itemKey={card.workItem.key}
                  className="btn-ghost"
                >
                  Open {card.workItem.key}
                </ItemLink>
              ) : null}
              {card.sprint ? (
                <Link
                  className="btn-ghost"
                  to={routePath('sprintRetro', { key: project.key, sprintId: card.sprint.id })}
                >
                  Sprint retro
                </Link>
              ) : null}
            </div>
          ) : null}
          <article
            data-testid={shared.retroCard}
            data-feature={isFeature ? 'true' : 'false'}
            className={`grid min-w-0 gap-2 rounded-md border border-edge bg-panel p-3 ${isFeature ? 'border-l-[3px] border-l-signal' : ''}`}
          >
            <SelectedHeader card={card} />
            <div className="[&>div]:grid-cols-1!">
              <RetroBoard
                columns={cardColumns(card.points)}
                groupByTheme={false}
                filter={() => true}
                note={{ showItem: false, highlightedPointId: highlighted?.id }}
                add={target.add}
                scope={target.scope}
              />
            </div>
            <CompiledFrom items={card.compiledFrom} />
            <FailureLog key={`${card.id}:${inLog}`} log={card.failureLog} defaultOpen={inLog} />
          </article>
        </div>
      ) : (
        <p className="m-0 text-[length:var(--text-sm)] text-ink-muted">
          Select a point to see its whole retro.
        </p>
      )}
    </aside>
  );
}

/** Where a card's board writes: its item, else its sprint, else nowhere. */
function boardTarget(card: RetroCardDto): { add: AddTarget; scope: RetroActionScope } | undefined {
  if (card.workItem) {
    return {
      add: { kind: 'item', workItemId: card.workItem.id },
      scope: { kind: 'item', itemId: card.workItem.id },
    };
  }
  if (card.sprint) {
    return {
      add: cardAddTarget(card, card.sprint.id),
      scope: { kind: 'sprint', sprintId: card.sprint.id },
    };
  }
  return undefined;
}

import { Link } from 'react-router-dom';
import { routePath } from '../../../routes';
import { ItemLink } from '../../itemPeek/ItemLink';
import { Chip } from '../../../components/Chip';
import { TypeIcon } from '../../../components/TypeIcon';
import { retroSharedTestIds as shared } from '../../retroShared/testIds';
import { AuthorChip } from '../../retroShared/AuthorChip';
import { RetroBoard } from '../../retroShared/board/RetroBoard';
import { SelectedLog } from './parts/SelectedLog';
import { cardAddTarget, cardColumns } from '../sprint/views/byWorkItem/cardColumns';
import { retrosTestIds as ids } from '../testIds';
import { highlightedRow } from './parts/matches';
import type { RetrosSearchSlotProps } from './SearchSlotProps';

/** The whole retro of the selected point as a board of the same notes and buttons as everywhere else, the clicked point highlighted, and the raw log open when that point matched only there. */
export function SelectedRetro({ project, search, pointId }: RetrosSearchSlotProps) {
  const card = search.selected;
  const highlighted = highlightedRow(search, pointId);
  const inLog = highlighted !== undefined && search.logMatchPointIds.includes(highlighted.id);
  return (
    <aside
      data-testid={ids.retrosSelected}
      aria-label="Retrospective"
      className="sticky top-[8.5rem] max-h-[calc(100vh-12rem)] min-w-0 self-start overflow-y-auto"
    >
      {card ? (
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
            className="grid min-w-0 gap-2 rounded-md border border-edge bg-panel p-3"
          >
            <header className="flex flex-wrap items-center gap-1.5 text-[length:var(--text-sm)]">
              {card.workItem ? (
                <>
                  <TypeIcon type={card.workItem.type.id} />
                  <span className="font-mono">{card.workItem.key}</span>
                  <b className="text-[length:var(--text-md)]">{card.workItem.title}</b>
                </>
              ) : (
                <>
                  <Chip>Sprint</Chip>
                  <b className="text-[length:var(--text-md)]">{card.sprint?.name}</b>
                </>
              )}
            </header>
            <div className="flex flex-wrap items-center gap-2 text-[length:var(--text-sm)]">
              <AuthorChip author={card.author} />
              <span className="text-[length:var(--text-xs)] text-ink-muted">
                {card.createdAtLabel}
              </span>
            </div>
            <div className="[&>div]:grid-cols-1!">
              <RetroBoard
                columns={cardColumns(card.points)}
                groupByTheme={false}
                filter={() => true}
                note={{ showItem: false, highlightedPointId: highlighted?.id }}
                add={cardAddTarget(card, card.sprint?.id ?? '')}
                scope={
                  card.workItem
                    ? { kind: 'item', itemId: card.workItem.id }
                    : { kind: 'sprint', sprintId: card.sprint?.id ?? '' }
                }
              />
            </div>
            <SelectedLog key={`${card.id}:${inLog}`} log={card.failureLog} defaultOpen={inLog} />
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

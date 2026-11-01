import { Link } from 'react-router-dom';
import { routePath } from '../../../routes';
import { ItemLink } from '../../itemPeek/ItemLink';
import { RetroCard } from '../../retroShared/RetroCard';
import { retrosTestIds as ids } from '../testIds';
import { highlightedRow } from './parts/matches';
import type { RetrosSearchSlotProps } from './SearchSlotProps';

/** The whole retro of the selected point: its card with the clicked point highlighted, and the raw log open when that point matched only there. */
export function SelectedRetro({ project, search, pointId }: RetrosSearchSlotProps) {
  const card = search.selected;
  const highlighted = highlightedRow(search, pointId);
  const inLog = highlighted !== undefined && search.logMatchPointIds.includes(highlighted.id);
  return (
    <aside data-testid={ids.retrosSelected} aria-label="Retrospective" className="min-w-0">
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
          <RetroCard
            key={`${card.id}:${inLog}`}
            card={card}
            highlightPointId={highlighted?.id}
            logDefaultOpen={inLog}
            hideEmptyCategories
          />
        </div>
      ) : (
        <p className="m-0 text-[length:var(--text-sm)] text-ink-muted">
          Select a point to see its whole retro.
        </p>
      )}
    </aside>
  );
}

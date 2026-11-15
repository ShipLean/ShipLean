import { useEffect, useRef } from 'react';
import { Link } from 'react-router-dom';
import { Dialog } from '../../../../components/Dialog';
import { routePath } from '../../../../routes';
import { ItemLink } from '../../../itemPeek/ItemLink';
import { RetroBoard } from '../../../retroShared/board/RetroBoard';
import { retroSharedTestIds as shared } from '../../../retroShared/testIds';
import { FailureLog } from '../../sprint/views/byWorkItem/FailureLog';
import { cardColumns } from '../../sprint/views/byWorkItem/cardColumns';
import { retrosTestIds as ids } from '../../testIds';
import { CompiledFrom } from '../parts/CompiledFrom';
import { SelectedHeader } from '../parts/SelectedHeader';
import { useStepThroughResults } from './useStepThroughResults';
import type { RetroPointDto } from '@shiplean/core/api/retros';
import type { RetroActionScope } from '../../../retroShared/actionDialog/RetroActionScope';
import type { AddTarget } from '../../../retroShared/board/AddTarget';
import type { RetrosSearchSlotProps } from '../SearchSlotProps';

const stepButton =
  'grid size-[var(--touch-target)] place-items-center rounded-control text-ink disabled:text-ink-muted disabled:opacity-40';

/** The open retro on a phone: the whole retro in a full-screen dialog, ‹ › stepping through the results. */
export function OpenRetroView({
  search,
  pointId,
  project,
  target,
  highlighted,
  inLog,
}: Pick<RetrosSearchSlotProps, 'search' | 'pointId' | 'project'> & {
  target: { add: AddTarget; scope: RetroActionScope };
  highlighted: RetroPointDto | undefined;
  inLog: boolean;
}) {
  const card = search.selected;
  const steps = useStepThroughResults(search, pointId);
  const body = useRef<HTMLDivElement>(null);
  const highlightedId = highlighted?.id;
  // The outlined point comes into view whenever the open point changes.
  useEffect(() => {
    body.current?.querySelector('[data-highlighted="true"]')?.scrollIntoView({ block: 'center' });
  }, [card?.id, highlightedId]);
  if (!card) return null;

  const item = card.workItem;
  const title = item
    ? `${item.type.name} ${item.key} ${item.title}`
    : `Sprint ${card.sprint?.name}`;
  const sprint = card.sprint?.name;
  const position = steps.position > 0 ? `Result ${steps.position} of ${steps.total}` : '';
  const isFeature = item?.type.id === 'feature';

  return (
    <Dialog
      title={title}
      onClose={steps.close}
      testId={ids.retrosOpenRetro}
      primaryAction={
        <>
          <button
            type="button"
            data-testid={ids.retrosOpenRetroPrevious}
            aria-label="Previous result"
            className={stepButton}
            disabled={!steps.canPrevious}
            onClick={steps.previous}
          >
            ‹
          </button>
          <button
            type="button"
            data-testid={ids.retrosOpenRetroNext}
            aria-label="Next result"
            className={stepButton}
            disabled={!steps.canNext}
            onClick={steps.next}
          >
            ›
          </button>
        </>
      }
    >
      <div ref={body} data-testid={ids.retrosSelected} className="grid min-w-0 gap-3">
        <p
          data-testid={ids.retrosOpenRetroPosition}
          className="m-0 text-[length:var(--text-sm)] text-ink-muted"
        >
          {[position, sprint].filter(Boolean).join(' · ')}
        </p>
        <article
          data-testid={shared.retroCard}
          data-feature={isFeature ? 'true' : 'false'}
          className="grid min-w-0 gap-3"
        >
          <SelectedHeader card={card} bare />
          <div className="flex flex-wrap gap-2">
            {item ? (
              <ItemLink projectKey={project.key} itemKey={item.key} className="btn-ghost">
                Open {item.key}
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
          <div className="[&>div]:grid-cols-1!">
            <RetroBoard
              columns={cardColumns(card.points)}
              groupByTheme={false}
              filter={() => true}
              note={{ showItem: false, highlightedPointId: highlightedId }}
              add={target.add}
              scope={target.scope}
            />
          </div>
          <CompiledFrom items={card.compiledFrom} />
          <FailureLog key={`${card.id}:${inLog}`} log={card.failureLog} defaultOpen={inLog} />
        </article>
      </div>
    </Dialog>
  );
}

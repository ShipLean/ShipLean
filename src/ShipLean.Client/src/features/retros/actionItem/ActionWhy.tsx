import { ApiRequestError } from '@shiplean/core/api/client';
import type { WorkItemTypeId } from '@shiplean/core/api/refs';
import { useRetroActionDialog } from '../../retroShared/actionDialog/useRetroActionDialog';
import { RetroQueryState } from '../RetroQueryState';
import { retrosTestIds as ids } from '../testIds';
import { useRetroAction } from '../useRetroQueries';
import { QuotedPoint } from './QuotedPoint';
import { useUpdateRetroAction } from './useUpdateRetroAction';
import { WhyFixSelect } from './WhyFixSelect';
import { WhyThemeSelect } from './WhyThemeSelect';

/**
 * "Why this exists" on an action item's page: the points it covers, quoted with links back, its theme and type of fix, and
 * + Add points. Only an action shows it. Each select PUTs both current values with the one that changed (K5), so both stay disabled until a write and the re-read after it have finished: a second change would otherwise send the first one's old value.
 */
export function ActionWhy({
  projectId,
  projectKey,
  workItemId,
  itemType,
}: {
  projectId: string;
  projectKey: string;
  workItemId: string;
  itemType: WorkItemTypeId;
}) {
  const isAction = itemType === 'action';
  const query = useRetroAction(projectId, workItemId, isAction);
  const update = useUpdateRetroAction(projectId, workItemId);
  const dialog = useRetroActionDialog();
  if (!isAction) return null;
  // An action item made without Why this exists has no retro_action row: the 404 is "nothing to show", not a failure.
  if (query.error instanceof ApiRequestError && query.error.status === 404) return null;
  return (
    <RetroQueryState query={query}>
      {({ action, theme, points }) => (
        <section
          data-testid={ids.retroActionWhy}
          aria-label="Why this exists"
          className="grid min-w-0 gap-2"
        >
          <header className="flex flex-wrap items-center gap-2">
            <h2 className="m-0 text-[length:var(--text-md)] font-semibold">Why this exists</h2>
            <span className="inline-flex h-4 items-center rounded-full bg-signal-soft px-[7px] text-[length:var(--text-xs)] font-medium whitespace-nowrap text-ink">
              {points.length} {points.length === 1 ? 'point' : 'points'}
            </span>
            <span className="flex-1" />
            <WhyThemeSelect
              projectId={projectId}
              theme={theme}
              disabled={update.isPending}
              onChange={(themeId) =>
                update.mutate({ fixType: action.fixType, themeId, themeName: null })
              }
            />
            <WhyFixSelect
              fixType={action.fixType}
              disabled={update.isPending}
              onChange={(fixType) =>
                update.mutate({ fixType, themeId: theme?.id ?? null, themeName: null })
              }
            />
            <button
              type="button"
              data-testid={ids.retroActionWhyAdd}
              className="h-6 cursor-pointer rounded-control border border-edge bg-panel px-2 text-[length:var(--text-xs)] text-ink"
              onClick={() =>
                dialog.open({ existingWorkItemId: workItemId, scope: { kind: 'project' } })
              }
            >
              + Add points
            </button>
          </header>
          <div className="grid grid-cols-[repeat(auto-fill,minmax(260px,1fr))] gap-2">
            {points.map((point) => (
              <QuotedPoint key={point.id} projectKey={projectKey} point={point} />
            ))}
          </div>
        </section>
      )}
    </RetroQueryState>
  );
}

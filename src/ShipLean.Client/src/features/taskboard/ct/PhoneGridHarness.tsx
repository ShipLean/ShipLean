import { useState } from 'react';
import { FeatureBoardSections } from '../../featureBoard/FeatureBoardSections';
import { FEATURE_VIEW, PARENT_VIEW } from '../../featureBoard/ct/fixtures';
import { TaskboardDnd } from '../moves/TaskboardDnd';
import type { TaskboardGrouping } from '../taskboardApi';
import type { TaskboardScope } from '../taskboardScope';
import { PROJECT } from './fixtures';

const scope: TaskboardScope = { kind: 'feature', featureId: 'f1' };

/**
 * The shared grid on a phone through the Feature board's sections: a leaf Feature (`feature`) or a parent one (`parent`).
 * Collapsing a lane or a band works as on the page (a mount cannot carry functions or a Set, so faces are an array).
 */
export function PhoneGridHarness({
  board,
  grouping = 'stories',
  faces = [],
}: {
  board: 'feature' | 'parent';
  grouping?: TaskboardGrouping;
  faces?: string[];
}) {
  const view = board === 'parent' ? PARENT_VIEW : FEATURE_VIEW;
  const [toggled, setToggled] = useState<ReadonlyMap<string, boolean>>(new Map());
  const [closedSections, setClosedSections] = useState<ReadonlySet<string>>(new Set());
  const isCollapsed = (rowId: string, completed: boolean) => toggled.get(rowId) ?? completed;
  return (
    <TaskboardDnd
      projectId={PROJECT.id}
      scope={scope}
      view={view}
      grouping={grouping}
      me={undefined}
      onLanded={() => undefined}
    >
      <FeatureBoardSections
        projectKey={PROJECT.key}
        projectId={PROJECT.id}
        scope={scope}
        view={view}
        grouping={grouping}
        faces={new Set(faces)}
        me={undefined}
        closedSections={closedSections}
        onToggleSection={(id) =>
          setClosedSections((now) => {
            const next = new Set(now);
            if (!next.delete(id)) next.add(id);
            return next;
          })
        }
        isCollapsed={isCollapsed}
        onToggle={(rowId, completed) =>
          setToggled((now) => new Map(now).set(rowId, !isCollapsed(rowId, completed)))
        }
        onSetState={() => undefined}
      />
    </TaskboardDnd>
  );
}

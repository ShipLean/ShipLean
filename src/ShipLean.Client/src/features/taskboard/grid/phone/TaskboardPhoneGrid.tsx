import { Fragment, useState, type ReactNode } from 'react';
import type { SprintRef, UserRef } from '@shiplean/core/api/refs';
import type { TaskboardRowDto } from '@shiplean/core/api/views';
import { StateDot } from '../../../../components/StateDot';
import { TouchPager } from '../../../../touch/TouchPager';
import type { TaskboardGrouping } from '../../taskboardApi';
import type { TaskboardGridView } from '../../taskboardGridView';
import type { TaskboardScope } from '../../taskboardScope';
import { taskboardTestIds as ids } from '../../testIds';
import { ClosedStoryLine } from './ClosedStoryLine';
import { PhonePersonLane } from './PhonePersonLane';
import { PhoneStoryLane } from './PhoneStoryLane';
import { phoneLanes } from './phoneLanes';
import { phonePeopleLanes } from './phonePeopleLanes';
import { stateChips } from './stateChips';
import { TaskboardPagerContext, type TaskboardPager } from './TaskboardPagerContext';

type Row = TaskboardRowDto & { sprint?: SprintRef | null };

/** A run of lanes on every page: a Feature's (after its band) on a parent Feature, or the whole grid's (no band). */
export interface PhoneGridSection {
  id: string;
  /** Drawn first on every page; null for none. */
  band: ReactNode;
  /** A collapsed band: its lanes are left out on every page. */
  closed: boolean;
  rows: Row[];
  /** Drawn instead of lanes when `rows` is empty. */
  empty?: ReactNode;
}

/**
 * The shared Taskboard grid at 900px and below (SHIP-722 D1, D4, D8, D11): one task state a page in the touch pager, with
 * state chips above. Grouped by Stories every open story is a lane on every page, closed stories one line each; grouped by
 * People the people are the lanes.
 */
export function TaskboardPhoneGrid({
  projectKey,
  projectId,
  scope,
  view,
  sections,
  grouping,
  faces,
  me,
  isCollapsed,
  onToggle,
  onSetState,
  label,
}: {
  projectKey: string;
  projectId: string;
  scope: TaskboardScope;
  view: TaskboardGridView;
  sections: PhoneGridSection[];
  grouping: TaskboardGrouping;
  faces: ReadonlySet<string>;
  me: UserRef | undefined;
  isCollapsed: (rowId: string, completed: boolean) => boolean;
  onToggle: (rowId: string, completed: boolean) => void;
  onSetState: (parentId: string, key: string, revision: number, stateId: string) => void;
  label: string;
}) {
  const pages = stateChips(
    view.columns,
    grouping === 'stories' ? sections.flatMap((s) => s.rows) : view.peopleRows,
    faces,
  );
  const [chosen, setChosen] = useState<string | undefined>(undefined);
  const current = pages.some((p) => p.id === chosen) ? chosen! : (pages[0]?.id ?? '');
  const index = pages.findIndex((p) => p.id === current);
  const pager: TaskboardPager = {
    stateId: current,
    turn: (dir) => setChosen(pages[Math.min(Math.max(index + dir, 0), pages.length - 1)]?.id),
  };

  const storyLanes = (section: PhoneGridSection, stateId: string) => {
    const { lanes, closed } = phoneLanes(section.rows, stateId, faces);
    return (
      <>
        {lanes.map((lane) => (
          <PhoneStoryLane
            key={lane.rowId}
            projectKey={projectKey}
            projectId={projectId}
            scope={scope}
            view={view}
            lane={lane}
            stateId={stateId}
            first={stateId === pages[0]?.id}
            collapsed={isCollapsed(lane.rowId, false)}
            onToggle={() => onToggle(lane.rowId, false)}
            onSetState={(next) =>
              lane.row.parent &&
              onSetState(lane.row.parent.id, lane.row.parent.key, lane.row.parent.revision, next)
            }
          />
        ))}
        {closed.map((story) => (
          <ClosedStoryLine key={story.rowId} projectKey={projectKey} story={story} />
        ))}
      </>
    );
  };

  const sectionBody = (section: PhoneGridSection, stateId: string) =>
    section.rows.length === 0 ? section.empty : storyLanes(section, stateId);

  const renderPage = (stateId: string) => {
    const column = view.columns.find((c) => c.state.id === stateId);
    const page = pages.find((p) => p.id === stateId);
    return (
      <div className="grid grid-cols-[minmax(0,1fr)] content-start gap-2 pt-1">
        <div
          data-testid={ids.phonePageHead}
          className="flex min-h-9 items-center gap-2 rounded-md border border-edge bg-panel px-2.5 font-semibold"
        >
          {column ? <StateDot category={column.state.category} name={column.state.name} /> : null}
          <span className="font-normal text-ink-muted">{page?.count}</span>
        </div>
        {grouping === 'stories'
          ? sections.map((section) => (
              <Fragment key={section.id}>
                {section.band}
                {section.closed ? null : sectionBody(section, stateId)}
              </Fragment>
            ))
          : phonePeopleLanes(view.peopleRows, stateId, faces).map((lane) => (
              <PhonePersonLane
                key={lane.rowId}
                view={view}
                lane={lane}
                stateId={stateId}
                meId={me?.id}
              />
            ))}
      </div>
    );
  };

  return (
    <TaskboardPagerContext.Provider value={pager}>
      <div data-testid={ids.phoneGrid} role="region" aria-label={label} className="min-w-0">
        <TouchPager
          pages={pages}
          current={current}
          onPageChange={setChosen}
          renderPage={renderPage}
        />
      </div>
    </TaskboardPagerContext.Provider>
  );
}

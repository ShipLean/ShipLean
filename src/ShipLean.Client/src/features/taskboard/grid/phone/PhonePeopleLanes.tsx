import { Fragment } from 'react';
import type { UserRef } from '@shiplean/core/api/refs';
import { useProjectMembers } from '../../../workItem/workItemApi';
import type { TaskboardGridView } from '../../taskboardGridView';
import { PhonePersonLane } from './PhonePersonLane';
import { phonePeopleLanes } from './phonePeopleLanes';

interface LanesProps {
  view: TaskboardGridView;
  stateId: string;
  faces: ReadonlySet<string>;
  meId: string | undefined;
}

function Lanes({ everyone, ...props }: LanesProps & { everyone: readonly UserRef[] | null }) {
  const { view, stateId, faces, meId } = props;
  return (
    <Fragment>
      {phonePeopleLanes(view.peopleRows, stateId, faces, everyone).map((lane) => (
        <PhonePersonLane
          key={lane.rowId}
          view={view}
          lane={lane}
          stateId={stateId}
          meId={meId}
          emptyLane={everyone !== null}
        />
      ))}
    </Fragment>
  );
}

/** One state's People lanes of the sprint Taskboard: everyone on the project, so the members are read here and nowhere else. */
export function SprintPeopleLanes({ projectId, ...props }: LanesProps & { projectId: string }) {
  const members = useProjectMembers(projectId).data;
  return <Lanes {...props} everyone={members ? members.map((m) => m.user) : null} />;
}

/** One state's People lanes of the Feature board: the people the view holds, as before. */
export function FeaturePeopleLanes(props: LanesProps) {
  return <Lanes {...props} everyone={null} />;
}

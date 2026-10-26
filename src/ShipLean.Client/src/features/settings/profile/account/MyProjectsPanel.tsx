import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Fragment, useRef, useState } from 'react';
import { showToast } from '../../../../components/toastStore';
import { fetchProjects, meKey, projectsKey } from '../../../../queries/shared';
import { leaveProject } from '../../settingsApi';
import { settingsTestIds } from '../../testIds';
import { errorText } from './errorText';
import { LeaveConfirm } from './LeaveConfirm';
import { MyProjectRow } from './MyProjectRow';
import { PanelFrame } from './PanelFrame';

export function MyProjectsPanel() {
  const queryClient = useQueryClient();
  const projects = useQuery({ queryKey: projectsKey, queryFn: fetchProjects });
  const [leavingId, setLeavingId] = useState<string | null>(null);
  const listRef = useRef<HTMLUListElement>(null);
  // The Leave and Cancel buttons go away with the question, so focus lands on the list.
  const focusList = () => listRef.current?.focus();
  const leave = useMutation({
    mutationFn: leaveProject,
    onSuccess: async (_none, projectId) => {
      const left = projects.data?.find((p) => p.id === projectId);
      setLeavingId(null);
      focusList();
      showToast(`You left ${left?.key ?? 'the project'}`, { tone: 'ok' });
      await queryClient.invalidateQueries({ queryKey: projectsKey });
      await queryClient.invalidateQueries({ queryKey: meKey });
    },
  });
  const list = projects.data;
  return (
    <PanelFrame
      title="Your projects"
      meta={
        list === undefined
          ? undefined
          : `${list.length} · admins add people from Project settings › Members`
      }
      testId={settingsTestIds.myProjectsPanel}
    >
      {list === undefined ? (
        <p
          data-testid={
            projects.isError
              ? settingsTestIds.myProjectsLoadFailed
              : settingsTestIds.myProjectsLoading
          }
          className="p-3 text-ink-muted"
        >
          {projects.isError ? 'Your projects could not be loaded.' : 'Loading…'}
        </p>
      ) : (
        <ul
          ref={listRef}
          tabIndex={-1}
          aria-label="Your projects"
          data-testid={settingsTestIds.myProjectsList}
          className="outline-0"
        >
          {list.map((project) => (
            <Fragment key={project.id}>
              <MyProjectRow
                project={project}
                confirming={leavingId === project.id}
                onLeave={() => {
                  leave.reset();
                  setLeavingId(project.id);
                }}
              />
              {leavingId === project.id ? (
                <LeaveConfirm
                  project={project}
                  error={leave.isError ? errorText(leave.error, 'You could not leave.') : null}
                  leaving={leave.isPending}
                  onConfirm={() => leave.mutate(project.id)}
                  onCancel={() => {
                    leave.reset();
                    setLeavingId(null);
                    focusList();
                  }}
                />
              ) : null}
            </Fragment>
          ))}
        </ul>
      )}
    </PanelFrame>
  );
}

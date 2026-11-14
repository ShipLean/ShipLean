import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { useLocation } from 'react-router-dom';
import { ToastHost } from '../../../components/ToastHost';
import type { ProjectDto } from '@shiplean/core/api/projects';
import type { BacklogMode, BacklogViewDto } from '@shiplean/core/api/views';
import { backlogKey, useBacklog } from '../backlogApi';
import { BulkRunProvider } from '../bulk/BulkRunProvider';
import { ListCommandsProvider } from '../commands/listCommands';
import { SelectionProvider } from '../selection/SelectionProvider';
import { TouchSelectModeProvider } from '../touch/selectMode';
import { TouchRowList } from '../touch/TouchRowList';
import { rowActionsHarnessIds } from './rowActionsHarnessIds';

const NONE: ReadonlySet<string> = new Set();

function Rows({ project, tree, mode }: { project: ProjectDto; tree: boolean; mode: BacklogMode }) {
  const backlog = useBacklog({
    projectId: project.id,
    level: 'stories',
    mode,
    filter: '',
    includeCompleted: false,
  });
  const { pathname, search } = useLocation();
  if (!backlog.isSuccess) return null;
  const list = (
    <TouchRowList
      rows={[...backlog.data.rows, ...backlog.data.sections.flatMap((section) => section.rows)]}
      projectKey={project.key}
      handles={false}
      flashing={NONE}
      filtered={false}
    />
  );
  return (
    <>
      <output data-testid={rowActionsHarnessIds.address}>{pathname + search}</output>
      {tree ? list : <SelectionProvider>{list}</SelectionProvider>}
    </>
  );
}

// CT-only: touch rows with the row ⋯ under the bulk runner and the touch Select mode, as List mounts them. `tree`
// leaves the SelectionProvider out, as Tree mode does (no Points, no Select).
export function RowActionsHarness({
  project,
  tree = false,
  mode = 'list',
  leftCached,
}: {
  project: ProjectDto;
  tree?: boolean;
  mode?: BacklogMode;
  // A read of the other mode left in the cache, created before the shown one (what switching modes leaves behind).
  leftCached?: BacklogViewDto;
}) {
  const queryClient = useQueryClient();
  useState(() => {
    if (leftCached) {
      queryClient.setQueryData(
        backlogKey(project.id, 'stories', leftCached.mode, '', false),
        leftCached,
      );
    }
    return null;
  });
  return (
    <ListCommandsProvider>
      <TouchSelectModeProvider>
        <BulkRunProvider
          projectId={project.id}
          queryKey={backlogKey(project.id, 'stories', mode, '', false)}
        >
          <ToastHost />
          <Rows project={project} tree={tree} mode={mode} />
        </BulkRunProvider>
      </TouchSelectModeProvider>
    </ListCommandsProvider>
  );
}

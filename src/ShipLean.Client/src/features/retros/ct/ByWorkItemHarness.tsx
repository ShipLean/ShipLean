import type { ProjectDto } from '../../../api/projects';
import type { SprintRetroDto } from '../../../api/retros';
import type { SprintDto } from '../../../api/sprints';
import { RetroActionDialogProvider } from '../../retroShared/actionDialog/RetroActionDialogProvider';
import { ByWorkItem } from '../sprint/views/ByWorkItem';

/** The By work item view under the dialog provider the panel gives it, with `decide` passed directly as the panel would. */
export function ByWorkItemHarness({
  project,
  sprint,
  retro,
  decide,
}: {
  project: ProjectDto;
  sprint: SprintDto;
  retro: SprintRetroDto;
  decide?: boolean;
}) {
  return (
    <RetroActionDialogProvider projectId={project.id}>
      <ByWorkItem project={project} sprint={sprint} retro={retro} decide={decide} />
    </RetroActionDialogProvider>
  );
}

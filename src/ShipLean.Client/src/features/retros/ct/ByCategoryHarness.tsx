import type { ProjectDto } from '../../../api/projects';
import type { SprintRetroDto } from '../../../api/retros';
import type { SprintDto } from '../../../api/sprints';
import { RetroActionDialogProvider } from '../../retroShared/actionDialog/RetroActionDialogProvider';
import { ByCategory } from '../sprint/views/ByCategory';

/** The By category view under the dialog provider the panel gives it, so a note's Action item has somewhere to open. */
export function ByCategoryHarness({
  project,
  sprint,
  retro,
  point,
}: {
  project: ProjectDto;
  sprint: SprintDto;
  retro: SprintRetroDto;
  point?: string;
}) {
  return (
    <RetroActionDialogProvider projectId={project.id}>
      <ByCategory project={project} sprint={sprint} retro={retro} point={point} />
    </RetroActionDialogProvider>
  );
}

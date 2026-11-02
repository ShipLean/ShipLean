import type { ProjectDto } from '../../../api/projects';
import { RetroActionDialogProvider } from '../../retroShared/actionDialog/RetroActionDialogProvider';
import { retrosTestIds as ids } from '../testIds';

// TODO: the Features table replaces this empty panel.
export function RetroFeaturesPanel({ project }: { project: ProjectDto }) {
  return (
    <RetroActionDialogProvider projectId={project.id}>
      <div data-testid={ids.retroFeaturesPanel} data-project={project.key} />
    </RetroActionDialogProvider>
  );
}

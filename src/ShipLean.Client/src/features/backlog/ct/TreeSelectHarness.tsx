import type { ProjectDto } from '@shiplean/core/api/projects';
import { TreeMode } from '../tree/TreeMode';
import { TouchSelectModeProvider, useTouchSelectMode } from '../touch/selectMode';

function StartSelect() {
  const { start } = useTouchSelectMode();
  return (
    <button type="button" data-testid="harness-select" onClick={start}>
      Select
    </button>
  );
}

// CT-only: Tree mode inside the touch Select mode provider (BacklogSlots mounts it in the app), with a button that turns it on.
export function TreeSelectHarness({ project }: { project: ProjectDto }) {
  return (
    <TouchSelectModeProvider>
      <StartSelect />
      <TreeMode project={project} />
    </TouchSelectModeProvider>
  );
}

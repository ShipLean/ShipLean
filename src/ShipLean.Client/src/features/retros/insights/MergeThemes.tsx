import { useState } from 'react';
import type { ProjectDto } from '../../../api/projects';
import { RetroQueryState } from '../RetroQueryState';
import { retrosTestIds as ids } from '../testIds';
import { useRetroThemes } from '../useRetroQueries';
import type { RetroInsightsSlotProps } from './InsightsSlotProps';
import { MergeDialog } from './merge/MergeDialog';

// Loads the project's themes only once the dialog is open.
function MergeDialogLoader({ project, onClose }: { project: ProjectDto; onClose: () => void }) {
  const themes = useRetroThemes(project.id);
  return (
    <RetroQueryState query={themes}>
      {(data) => <MergeDialog project={project} options={data.themes} onClose={onClose} />}
    </RetroQueryState>
  );
}

/** The Merge themes… button and its dialog. */
export function MergeThemes({ project }: Pick<RetroInsightsSlotProps, 'project'>) {
  const [open, setOpen] = useState(false);
  return (
    <>
      <button
        type="button"
        data-testid={ids.retroMergeThemes}
        className="cursor-pointer rounded-control border border-edge bg-panel px-3 py-1 text-[length:var(--text-sm)]"
        onClick={() => setOpen(true)}
      >
        Merge themes…
      </button>
      {open ? <MergeDialogLoader project={project} onClose={() => setOpen(false)} /> : null}
    </>
  );
}

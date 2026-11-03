import { useState } from 'react';
import type { ProjectDto } from '../../../api/projects';
import { RetroQueryState } from '../RetroQueryState';
import { retrosTestIds as ids } from '../testIds';
import { useRetroThemes } from '../useRetroQueries';
import type { RetroInsightsSlotProps } from './InsightsSlotProps';
import { MergeDialog } from './merge/MergeDialog';
import { MergeProposals } from './merge/MergeProposals';

// Loads the project's themes only once the dialog is open.
function MergeDialogLoader({ project, onClose }: { project: ProjectDto; onClose: () => void }) {
  const themes = useRetroThemes(project.id);
  return (
    <RetroQueryState query={themes}>
      {(data) => <MergeDialog project={project} options={data.themes} onClose={onClose} />}
    </RetroQueryState>
  );
}

/** The Merge themes… button and its dialog, and under the toolbar the merges agents proposed. */
export function MergeThemes({ project, insights }: RetroInsightsSlotProps) {
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
      <div className="absolute inset-x-0 top-full z-10">
        <MergeProposals project={project} proposals={insights.mergeProposals} />
      </div>
      {open ? <MergeDialogLoader project={project} onClose={() => setOpen(false)} /> : null}
    </>
  );
}

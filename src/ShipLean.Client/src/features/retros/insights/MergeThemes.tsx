import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import type { ProjectDto } from '@shiplean/core/api/projects';
import { useTouchShell } from '../../../touch/useTouchShell';
import { RetroQueryState } from '../RetroQueryState';
import { retrosTestIds as ids } from '../testIds';
import { useRetroThemes } from '../useRetroQueries';
import type { RetroInsightsSlotProps } from './InsightsSlotProps';
import { MergeDialog } from './merge/MergeDialog';
import { readMerge } from './touch/insightsAddress';

// Loads the project's themes only once the dialog is open.
function MergeDialogLoader({
  project,
  initialSource,
  onClose,
}: {
  project: ProjectDto;
  initialSource?: string;
  onClose: () => void;
}) {
  const themes = useRetroThemes(project.id);
  return (
    <RetroQueryState query={themes}>
      {(data) => (
        <MergeDialog
          project={project}
          options={data.themes}
          initialSource={initialSource}
          onClose={onClose}
        />
      )}
    </RetroQueryState>
  );
}

/**
 * Merge themes. Above 900px the button and its dialog; at touch no button (⋯ has it) and the full-screen dialog is open
 * while the address has `merge` (`merge=<themeId>` ticks that theme).
 */
export function MergeThemes({ project }: Pick<RetroInsightsSlotProps, 'project'>) {
  const [open, setOpen] = useState(false);
  const { touch } = useTouchShell();
  const [params, setParams] = useSearchParams();
  if (touch) {
    const merge = readMerge(params);
    if (merge === null) return null;
    const close = () => {
      const next = new URLSearchParams(params);
      next.delete('merge');
      setParams(next, { replace: true });
    };
    return (
      <MergeDialogLoader
        project={project}
        initialSource={merge.themeId ?? undefined}
        onClose={close}
      />
    );
  }
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

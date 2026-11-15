import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTouchShell } from '../../../../touch/useTouchShell';
import { Dialog } from '../../../../components/Dialog';
import type { ProjectDto } from '@shiplean/core/api/projects';
import type { RetroThemeOptionDto } from '@shiplean/core/api/retros';
import { mergeRetroThemes } from '@shiplean/core/api/retrosApi';
import { retrosTestIds as ids } from '../../testIds';
import { retrosRootKey } from '../../useRetroQueries';

/** Pick the themes to fold in and the one that stays; the preview shows the combined count before it is sent. */
export function MergeDialog({
  project,
  options,
  initialSource,
  onClose,
}: {
  project: ProjectDto;
  options: readonly RetroThemeOptionDto[];
  /** A theme ticked when the dialog opens. */
  initialSource?: string;
  onClose: () => void;
}) {
  const { touch } = useTouchShell();
  const queryClient = useQueryClient();
  const [sources, setSources] = useState<readonly string[]>(
    initialSource !== undefined && options.some((o) => o.theme.id === initialSource)
      ? [initialSource]
      : [],
  );
  const [target, setTarget] = useState('');
  const [error, setError] = useState<string | null>(null);
  const merge = useMutation({
    mutationFn: () => mergeRetroThemes(project.id, { sourceIds: [...sources], targetId: target }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: retrosRootKey(project.id) });
      onClose();
    },
    onError: (failure) => setError(failure.message),
  });

  const toggle = (id: string, on: boolean) =>
    setSources((was) => (on ? [...was, id] : was.filter((s) => s !== id)));
  const chooseTarget = (id: string) => {
    setTarget(id);
    setSources((was) => was.filter((s) => s !== id));
  };

  const picked = options.filter((o) => sources.includes(o.theme.id));
  const into = options.find((o) => o.theme.id === target);
  const combined = picked.reduce((sum, o) => sum + o.pointCount, into?.pointCount ?? 0);
  const ready = picked.length > 0 && into !== undefined;

  const mergeButton = (
    <button
      type="button"
      data-testid={ids.retroMergeSave}
      disabled={!ready || merge.isPending}
      className="h-[var(--touch-target)] cursor-pointer px-3 text-[length:var(--text-base)] font-semibold text-signal disabled:cursor-default disabled:text-ink-muted disabled:opacity-60"
      onClick={() => merge.mutate()}
    >
      Merge
    </button>
  );

  return (
    <Dialog
      title="Merge themes"
      onClose={onClose}
      testId={ids.retroMergeDialog}
      primaryAction={mergeButton}
      footer={
        touch ? undefined : (
          <>
            <span className="flex-1" />
            <button
              type="button"
              className="cursor-pointer rounded-control border border-edge bg-panel px-3 py-1"
              onClick={onClose}
            >
              Cancel
            </button>
            <button
              type="button"
              data-testid={ids.retroMergeSave}
              disabled={!ready || merge.isPending}
              className="cursor-pointer rounded-control border border-edge bg-signal px-3 py-1 text-signal-ink disabled:opacity-50"
              onClick={() => merge.mutate()}
            >
              Merge
            </button>
          </>
        )
      }
    >
      <fieldset className="m-0 grid gap-1 border-0 p-0">
        <legend className="mb-1 text-[length:var(--text-sm)] font-semibold">Themes to merge</legend>
        {options.map((o) => (
          <label
            key={o.theme.id}
            className="flex items-center gap-2 text-[length:var(--text-sm)] touch:min-h-10"
          >
            <input
              type="checkbox"
              data-testid={ids.retroMergeSource}
              data-theme-id={o.theme.id}
              checked={sources.includes(o.theme.id)}
              disabled={o.theme.id === target}
              onChange={(event) => toggle(o.theme.id, event.target.checked)}
            />
            <span className="flex-1">{o.theme.name}</span>
            <span className="text-ink-muted tabular-nums">{o.pointCount}</span>
          </label>
        ))}
      </fieldset>
      <label className="grid gap-1 text-[length:var(--text-sm)]">
        <span className="font-semibold">Into</span>
        <select
          data-testid={ids.retroMergeTarget}
          value={target}
          className="rounded-control border border-edge bg-panel px-2 py-1 touch:h-10"
          onChange={(event) => chooseTarget(event.target.value)}
        >
          <option value="">Choose the theme to keep</option>
          {options.map((o) => (
            <option key={o.theme.id} value={o.theme.id} disabled={sources.includes(o.theme.id)}>
              {o.theme.name}
            </option>
          ))}
        </select>
      </label>
      <p
        data-testid={ids.retroMergePreview}
        data-combined={ready ? combined : ''}
        className="m-0 text-[length:var(--text-sm)] text-ink-muted"
      >
        {ready
          ? `${picked.length} theme${picked.length === 1 ? '' : 's'} into "${into.theme.name}": ${combined} points combined`
          : 'Pick the themes to merge and the theme to keep.'}
      </p>
      {error ? (
        <p role="alert" data-error="" className="m-0 text-[length:var(--text-sm)] text-alert">
          {error}
        </p>
      ) : null}
    </Dialog>
  );
}

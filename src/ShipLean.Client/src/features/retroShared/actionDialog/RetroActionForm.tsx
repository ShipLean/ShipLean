import { useState } from 'react';
import type { RetroActionDialogDto, RetroFixType } from '@shiplean/core/api/retros';
import { Dialog } from '../../../components/Dialog';
import { TypeIcon } from '../../../components/TypeIcon';
import { useTouchShell } from '../../../touch/useTouchShell';
import { retroSharedTestIds as ids } from '../testIds';
import { commonestTheme } from './commonestTheme';
import { ExistingActionField } from './ExistingActionField';
import { FixTypePicker } from './FixTypePicker';
import { ModeToggle, type ActionMode } from './ModeToggle';
import { PointPicker } from './PointPicker';
import type { OpenRetroActionDialog } from './RetroActionDialogContext';
import { ThemeField } from './ThemeField';
import { useRetroActionDialog } from './useRetroActionDialog';
import { useSaveRetroAction } from './useSaveRetroAction';

const TITLE_SEED_LENGTH = 80;
const DEFAULT_FIX: RetroFixType = 'guide';

/**
 * The dialog itself, mounted once the offer has loaded. Its state starts from what was selected; a refetch only changes
 * what is listed. At 900px and below (SHIP-727 D5) it is full screen: Create (Add for an existing item) is the top
 * right `primaryAction`, New | Add to existing sits under the header, the "Starts in New" note ends the body, and there
 * is no footer (✕ cancels). Nothing is focused on open at any width.
 */
export function RetroActionForm({
  request,
  dialog,
  onClose,
}: {
  request: OpenRetroActionDialog;
  dialog: RetroActionDialogDto;
  onClose: () => void;
}) {
  const { projectId } = useRetroActionDialog();
  const { touch } = useTouchShell();
  const save = useSaveRetroAction(projectId);
  const [mode, setMode] = useState<ActionMode>(request.existingWorkItemId ? 'existing' : 'new');
  const [title, setTitle] = useState(() =>
    (dialog.selected[0]?.text ?? '').slice(0, TITLE_SEED_LENGTH),
  );
  const [fix, setFix] = useState<RetroFixType | null>(DEFAULT_FIX);
  const [chosenTheme, setChosenTheme] = useState<string | undefined>(undefined);
  const [checked, setChecked] = useState(() => new Set(dialog.selected.map((point) => point.id)));
  const [existingId, setExistingId] = useState(
    request.existingWorkItemId ?? dialog.openActions[0]?.workItemId ?? '',
  );

  const listed = [...dialog.selected, ...dialog.offered];
  const covered = listed.filter((point) => checked.has(point.id));
  const themeId = chosenTheme ?? commonestTheme(covered)?.id ?? '';
  const existing = dialog.openActions.some((action) => action.workItemId === existingId);
  const ready =
    covered.length > 0 &&
    !save.isPending &&
    (mode === 'new' ? title.trim() !== '' && fix !== null : existing);

  const toggle = (pointId: string) =>
    setChecked((now) => {
      const next = new Set(now);
      if (!next.delete(pointId)) next.add(pointId);
      return next;
    });

  const submit = () => {
    const pointIds = covered.map((point) => point.id);
    if (mode === 'new' && fix !== null) {
      save.mutate(
        {
          mode: 'new',
          body: {
            title: title.trim(),
            fixType: fix,
            pointIds,
            themeId: themeId || null,
            themeName: null,
          },
        },
        { onSuccess: onClose },
      );
    } else {
      save.mutate({ mode: 'existing', workItemId: existingId, pointIds }, { onSuccess: onClose });
    }
  };

  const startsNote =
    mode === 'new' ? 'Starts in New with no sprint. Pulling it into a sprint is the approval.' : '';
  const toggleModes = <ModeToggle mode={mode} onChange={setMode} />;
  const fields = (
    <>
      {mode === 'new' ? (
        <>
          <label className="grid gap-1">
            <span className="flex items-center gap-1.5 text-[length:var(--text-xs)] font-semibold text-ink-muted">
              Title
            </span>
            <input
              data-testid={ids.retroActionTitle}
              value={title}
              placeholder="What will change"
              className="field w-full touch:h-10"
              onChange={(e) => setTitle(e.target.value)}
            />
          </label>
          <FixTypePicker value={fix} onChange={setFix} />
          <ThemeField
            value={themeId}
            chosen={chosenTheme !== undefined}
            points={covered}
            onChoose={setChosenTheme}
          />
        </>
      ) : (
        <ExistingActionField
          actions={dialog.openActions}
          value={existingId}
          onChange={setExistingId}
        />
      )}
      <PointPicker
        selected={dialog.selected}
        offered={dialog.offered}
        checked={checked}
        onToggle={toggle}
      />
    </>
  );

  if (touch) {
    return (
      <Dialog
        title="Action item"
        onClose={onClose}
        testId={ids.retroActionDialog}
        titleIcon={<TypeIcon type="action" />}
        primaryAction={
          <button
            type="button"
            data-testid={ids.retroActionCreate}
            disabled={!ready}
            className="btn-signal h-[var(--touch-target)] cursor-pointer text-[length:var(--text-sm)] disabled:cursor-default disabled:opacity-60"
            onClick={submit}
          >
            {mode === 'new' ? 'Create' : 'Add'}
          </button>
        }
      >
        {toggleModes}
        {fields}
        {startsNote ? (
          <p
            data-testid={ids.retroActionStartsNote}
            className="m-0 text-[length:var(--text-sm)] text-ink-muted"
          >
            {startsNote}
          </p>
        ) : null}
      </Dialog>
    );
  }

  return (
    <Dialog
      title="Action item"
      width="620px"
      onClose={onClose}
      testId={ids.retroActionDialog}
      titleIcon={<TypeIcon type="action" />}
      headerExtra={toggleModes}
      footer={
        <>
          <span className="flex-1 text-[length:var(--text-xs)] text-ink-muted">{startsNote}</span>
          <button
            type="button"
            className="btn-ghost cursor-pointer border-transparent text-ink"
            onClick={onClose}
          >
            Cancel
          </button>
          <button
            type="button"
            data-testid={ids.retroActionSave}
            disabled={!ready}
            className="btn-signal cursor-pointer disabled:cursor-default disabled:opacity-60"
            onClick={submit}
          >
            {mode === 'new' ? 'Create action item' : 'Add points'}
          </button>
        </>
      }
    >
      {fields}
    </Dialog>
  );
}

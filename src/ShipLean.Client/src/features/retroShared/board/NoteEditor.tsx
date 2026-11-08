import { useEffect, useRef, useState } from 'react';
import type { RetroCategory, RetroPointDto } from '../../../api/retros';
import { showToast } from '../../../components/toastStore';
import { useRetroActionDialog } from '../actionDialog/useRetroActionDialog';
import { retroCategoryLabels } from '../retroCategories';
import { retroSharedTestIds as ids } from '../testIds';
import type { AddTarget } from './AddTarget';
import { useCreatePoint } from './useCreatePoint';

// At 900px and below (SHIP-727 D6) every field is 16px (K1's base rule), About stays a native select (C5) 40px tall,
// and Add and Cancel are 32px.
const fieldClass =
  'w-full resize-y rounded-[var(--radius-xs)] border border-edge bg-white px-1.5 py-1 text-[length:var(--text-xs)] text-ink touch:px-2 touch:py-1.5';
const miniClass =
  'h-[22px] cursor-pointer rounded-[var(--radius-xs)] border px-2 text-[length:var(--text-xs)] disabled:cursor-default disabled:opacity-60 touch:h-[var(--touch-target)] touch:px-3.5 touch:text-[length:var(--text-sm)]';

/** The blank note a column opens: text, a root cause on Went badly, an About choice in a sprint. Never names a retro: the server finds or makes the person's own. */
export function NoteEditor({
  category,
  add,
  onClose,
  onAdded,
}: {
  category: RetroCategory;
  add: AddTarget;
  onClose: () => void;
  onAdded?: (point: RetroPointDto) => void;
}) {
  const { projectId } = useRetroActionDialog();
  const create = useCreatePoint(projectId);
  const [text, setText] = useState('');
  const [cause, setCause] = useState('');
  const sprintOption =
    add.kind === 'sprint'
      ? (add.about.find((o) => o.value === '' || o.value === add.sprintId) ?? add.about[0])
      : undefined;
  const [about, setAbout] = useState(sprintOption?.value ?? '');
  const field = useRef<HTMLTextAreaElement>(null);
  useEffect(() => field.current?.focus(), []);
  const label = retroCategoryLabels[category];
  const body = text.trim();
  const root = cause.trim();

  async function save() {
    const onItem = add.kind === 'item' || about !== sprintOption?.value;
    try {
      const point = await create.mutateAsync({
        retroId: null,
        workItemId: add.kind === 'item' ? add.workItemId : onItem ? about : null,
        sprintId: add.kind === 'sprint' && !onItem ? add.sprintId : null,
        category,
        text: body,
        rootCause: category === 'wentBadly' && root !== '' ? root : null,
        themeId: null,
        themeName: null,
      });
      onClose();
      onAdded?.(point);
    } catch (failure) {
      showToast(failure instanceof Error ? failure.message : 'The note could not be added.', {
        tone: 'danger',
      });
    }
  }

  return (
    <article className="paper paper-postit paper-sheet relative grid min-w-0 gap-1.5 rounded-[2px] border px-2.5 py-2 text-[length:var(--text-xs)] touch:gap-2 touch:px-3 touch:py-2.5 touch:text-[length:var(--text-sm)]">
      <textarea
        ref={field}
        data-testid={ids.retroBoardAddText}
        rows={3}
        value={text}
        placeholder={`${label}: what happened?`}
        aria-label={`${label}: what happened?`}
        className={fieldClass}
        onChange={(event) => setText(event.target.value)}
      />
      {category === 'wentBadly' ? (
        <label className="grid gap-0.5 text-ink-muted">
          Root cause
          <textarea
            data-testid={ids.retroBoardAddCause}
            rows={2}
            value={cause}
            placeholder="Optional"
            className={fieldClass}
            onChange={(event) => setCause(event.target.value)}
          />
        </label>
      ) : null}
      {add.kind === 'sprint' ? (
        <label className="grid gap-0.5 text-ink-muted">
          About
          <select
            data-testid={ids.retroBoardAddAbout}
            value={about}
            className={`${fieldClass} touch:h-10`}
            onChange={(event) => setAbout(event.target.value)}
          >
            {add.about.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </label>
      ) : null}
      <div className="flex items-center gap-1.5 touch:gap-2">
        <button
          type="button"
          data-testid={ids.retroBoardAddSave}
          disabled={body === '' || create.isPending}
          className={`${miniClass} border-signal bg-signal font-semibold text-signal-ink`}
          onClick={() => void save()}
        >
          Add
        </button>
        <button
          type="button"
          data-testid={ids.retroBoardAddCancel}
          className={`${miniClass} border-edge bg-white text-ink`}
          onClick={onClose}
        >
          Cancel
        </button>
        <span className="flex-1" />
        <span className="text-ink-muted">Goes on your retro</span>
      </div>
    </article>
  );
}

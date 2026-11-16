import { useRef, useState } from 'react';
import type { RetroPointDto } from '@shiplean/core/api/retros';
import { ActionSheet, type SheetAction } from '../../../touch/ActionSheet';
import { Sheet } from '../../../touch/Sheet';
import { useRetroThemes } from '../../retros/useRetroQueries';
import { useRetroActionDialog } from '../actionDialog/useRetroActionDialog';
import { retroSharedTestIds as ids } from '../testIds';
import { NotePreview } from './NotePreview';
import type { RetroNoteWrites } from './useRetroNoteWrites';

/**
 * Move to theme as a bottom sheet (the touch layout): the point, then a list of the project's themes with ✓ on the current
 * one and its point count, Unthemed, and New theme…, which swaps the list for a name field. Picking moves and closes.
 */
export function NoteThemeSheet({
  open,
  point,
  writes,
  onClose,
}: {
  open: boolean;
  point: RetroPointDto;
  writes: RetroNoteWrites;
  onClose: () => void;
}) {
  const { projectId } = useRetroActionDialog();
  const themes = useRetroThemes(projectId).data;
  const [naming, setNaming] = useState(false);
  const [name, setName] = useState('');
  // A row closes the list after it runs; New theme… keeps the sheet going as the name step instead.
  const startedNaming = useRef(false);
  if (themes === undefined) return null;

  const current = point.theme?.id ?? null;
  const move = (pick: string | { name: string } | null) =>
    writes.moveTheme.mutate({ pointId: point.id, pick });
  const finish = () => {
    setNaming(false);
    setName('');
    onClose();
  };
  const typed = name.trim();
  const actions: SheetAction[] = [
    ...themes.themes.map((option) => ({
      id: option.theme.id,
      label: option.theme.name,
      detail: `${option.pointCount} ${option.pointCount === 1 ? 'point' : 'points'}`,
      checked: option.theme.id === current,
      onSelect: () => move(option.theme.id),
    })),
    { id: 'unthemed', label: 'Unthemed', checked: current === null, onSelect: () => move(null) },
    {
      id: 'new',
      label: 'New theme…',
      icon: '+',
      onSelect: () => {
        startedNaming.current = true;
        setNaming(true);
      },
    },
  ];
  return (
    <>
      <ActionSheet
        open={open && !naming}
        title="Move to theme"
        actions={actions}
        onClose={() => {
          if (startedNaming.current) startedNaming.current = false;
          else finish();
        }}
      >
        <NotePreview point={point} />
      </ActionSheet>
      <Sheet open={open && naming} title="New theme" onClose={finish}>
        <NotePreview point={point} />
        <form
          className="grid gap-2 pb-2"
          onSubmit={(event) => {
            event.preventDefault();
            if (typed === '') return;
            move({ name: typed });
            finish();
          }}
        >
          <input
            type="text"
            data-testid={ids.retroThemeSheetName}
            aria-label="Theme name"
            placeholder="Name the theme"
            value={name}
            className="h-10 rounded-control border border-edge bg-canvas px-2 text-[length:var(--text-md)] text-ink"
            onChange={(event) => setName(event.target.value)}
          />
          <button
            type="submit"
            data-testid={ids.retroThemeSheetAdd}
            disabled={typed === ''}
            className="btn-signal min-h-[var(--touch-target)] justify-center"
          >
            Add and move
          </button>
        </form>
      </Sheet>
    </>
  );
}

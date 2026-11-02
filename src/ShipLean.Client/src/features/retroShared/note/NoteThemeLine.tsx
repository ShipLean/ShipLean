import { useEffect, useRef, useState } from 'react';
import type { RetroPointDto } from '../../../api/retros';
import { useRetroThemes } from '../../retros/useRetroQueries';
import { useRetroActionDialog } from '../actionDialog/useRetroActionDialog';
import { ThemePicker } from '../ThemePicker';
import { retroSharedTestIds as ids } from '../testIds';
import type { RetroNoteWrites } from './useRetroNoteWrites';

// TODO: the finished theme line styling replaces this plain button.
export function NoteThemeLine({
  point,
  writes,
}: {
  point: RetroPointDto;
  writes: RetroNoteWrites;
}) {
  const { projectId } = useRetroActionDialog();
  const themes = useRetroThemes(projectId).data;
  const [picking, setPicking] = useState(false);
  const anchor = useRef<HTMLSpanElement>(null);
  useEffect(() => {
    if (!picking) return;
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setPicking(false);
    };
    const onDown = (event: MouseEvent) => {
      if (!anchor.current?.contains(event.target as Node)) setPicking(false);
    };
    document.addEventListener('keydown', onKey);
    document.addEventListener('mousedown', onDown);
    return () => {
      document.removeEventListener('keydown', onKey);
      document.removeEventListener('mousedown', onDown);
    };
  }, [picking]);
  return (
    <span ref={anchor} className="relative inline-block">
      <button
        type="button"
        data-testid={ids.retroNoteTheme}
        aria-expanded={picking}
        disabled={themes === undefined}
        className="cursor-pointer border-0 bg-transparent p-0 text-[length:var(--text-xs)] text-ink-muted hover:text-ink disabled:cursor-default"
        onClick={() => setPicking(!picking)}
      >
        # {point.theme?.name ?? 'Unthemed'} ▾
      </button>
      {picking && themes ? (
        <span className="absolute left-0 z-10 mt-1 block">
          <ThemePicker
            themes={themes.themes}
            mode={themes.mode}
            value={point.theme?.id ?? null}
            onPick={(pick) => {
              setPicking(false);
              writes.moveTheme.mutate({ pointId: point.id, pick });
            }}
          />
        </span>
      ) : null}
    </span>
  );
}

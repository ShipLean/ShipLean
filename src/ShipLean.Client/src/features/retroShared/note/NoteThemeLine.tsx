import { useEffect, useRef, useState } from 'react';
import type { RetroPointDto } from '@shiplean/core/api/retros';
import { useRetroThemes } from '../../retros/useRetroQueries';
import { useRetroActionDialog } from '../actionDialog/useRetroActionDialog';
import { ThemePicker } from '../ThemePicker';
import { retroSharedTestIds as ids } from '../testIds';
import type { RetroNoteWrites } from './useRetroNoteWrites';

/**
 * The note's `# theme` line. A tap opens the theme picker: a popover on desktop, or, with `onOpenSheet` (the touch
 * layout), whatever the caller opens, which is `NoteThemeSheet`.
 */
export function NoteThemeLine({
  point,
  writes,
  onOpenSheet,
}: {
  point: RetroPointDto;
  writes: RetroNoteWrites;
  onOpenSheet?: () => void;
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
    <span ref={anchor} className="relative max-w-full justify-self-start">
      <button
        type="button"
        data-testid={ids.retroNoteTheme}
        aria-expanded={onOpenSheet ? undefined : picking}
        aria-haspopup={onOpenSheet ? 'dialog' : undefined}
        disabled={themes === undefined}
        className={`cursor-pointer border-0 bg-transparent p-0 text-left text-[length:var(--text-xs)] text-ink-muted hover:text-ink hover:underline disabled:cursor-default touch:min-h-[var(--touch-target)] touch:min-w-[var(--touch-target)] touch:text-[length:var(--text-sm)] ${point.theme ? '' : 'italic'}`}
        onClick={onOpenSheet ?? (() => setPicking(!picking))}
      >
        # {point.theme?.name ?? 'Unthemed'} <span className="text-[9px] opacity-70">▾</span>
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

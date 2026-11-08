import { useEffect, useRef, useState } from 'react';
import type { RetroPointDto } from '../../../api/retros';
import type { RetroActionScope } from '../actionDialog/RetroActionScope';
import { useRetroActionDialog } from '../actionDialog/useRetroActionDialog';
import { useTouchShell } from '../../../touch/useTouchShell';
import { retroSharedTestIds as ids } from '../testIds';
import { NoteDecision } from './NoteDecision';
import { NoteMeta } from './NoteMeta';
import { NoteMore } from './NoteMore';
import { NoteThemeLine, NoteThemeSheet } from './NoteThemeLine';
import { useRetroNoteWrites } from './useRetroNoteWrites';

/**
 * A point as a post-it. It reads the themes and does its own writes, taking the project from the action dialog's
 * provider, so a screen passes only the point and where it sits. A highlighted note scrolls itself into view once.
 * At 900px and below (SHIP-727 D6, D8) its controls are 32px, ⋯ opens an action sheet (Move to theme, Archive) and
 * # theme opens Move to theme as a bottom sheet.
 */
export function RetroNote({
  point,
  showItem = false,
  showAuthor = true,
  showTheme = true,
  highlighted = false,
  scope,
}: {
  point: RetroPointDto;
  showItem?: boolean;
  showAuthor?: boolean;
  showTheme?: boolean;
  highlighted?: boolean;
  scope: RetroActionScope;
}) {
  const { projectId } = useRetroActionDialog();
  const writes = useRetroNoteWrites(projectId);
  const { touch } = useTouchShell();
  const [themeSheet, setThemeSheet] = useState(false);
  const openThemeSheet = touch ? () => setThemeSheet(true) : undefined;
  const note = useRef<HTMLElement>(null);
  const scrolled = useRef(false);
  useEffect(() => {
    if (!highlighted || scrolled.current) return;
    scrolled.current = true;
    note.current?.scrollIntoView({ block: 'center' });
  }, [highlighted]);
  return (
    <article
      ref={note}
      data-testid={ids.retroNote}
      data-point-id={point.id}
      data-category={point.category}
      data-decision={point.decision ?? 'none'}
      data-highlighted={highlighted ? 'true' : undefined}
      className="paper paper-postit paper-sheet relative grid min-w-0 grid-cols-[minmax(0,1fr)] gap-1.5 rounded-[2px] border px-2.5 py-2 text-[length:var(--text-sm)] touch:gap-2 touch:px-3 touch:py-2.5 touch:text-[length:var(--text-base)] [overflow-wrap:anywhere] [&[data-decision=notNeeded]>:not([data-testid=retro-note-none])]:opacity-[0.62] data-[highlighted=true]:outline-2 data-[highlighted=true]:outline-offset-1 data-[highlighted=true]:outline-[var(--theme-accent)]"
    >
      {point.canArchive ? (
        <NoteMore pointId={point.id} writes={writes} onMoveTheme={openThemeSheet} />
      ) : null}
      <p className={`m-0 ${point.canArchive ? 'pr-4 touch:pr-[30px]' : ''}`}>{point.text}</p>
      {point.rootCause ? (
        <p className="m-0 text-[length:var(--text-xs)] text-ink-muted touch:text-[length:var(--text-sm)]">
          <b>Root cause:</b> {point.rootCause}
        </p>
      ) : null}
      {showTheme ? (
        <NoteThemeLine point={point} writes={writes} onOpenSheet={openThemeSheet} />
      ) : null}
      {showItem || showAuthor ? (
        <NoteMeta point={point} showItem={showItem} showAuthor={showAuthor} />
      ) : null}
      <NoteDecision point={point} scope={scope} writes={writes} />
      {touch ? (
        <NoteThemeSheet
          open={themeSheet}
          point={point}
          writes={writes}
          onClose={() => setThemeSheet(false)}
        />
      ) : null}
    </article>
  );
}

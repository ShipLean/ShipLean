import { useEffect, useRef } from 'react';
import type { RetroPointDto } from '../../../api/retros';
import type { RetroActionScope } from '../actionDialog/RetroActionScope';
import { useRetroActionDialog } from '../actionDialog/useRetroActionDialog';
import { retroSharedTestIds as ids } from '../testIds';
import { NoteDecision } from './NoteDecision';
import { NoteMeta } from './NoteMeta';
import { NoteMore } from './NoteMore';
import { NoteThemeLine } from './NoteThemeLine';
import { useRetroNoteWrites } from './useRetroNoteWrites';

/**
 * A point as a post-it. It reads the themes and does its own writes, taking the project from the action dialog's
 * provider, so a screen passes only the point and where it sits. A highlighted note scrolls itself into view once.
 */
// TODO: the finished post-it styling replaces this plain note.
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
      className="relative grid min-w-0 gap-1.5 rounded-sm border border-edge bg-panel p-2 text-[length:var(--text-sm)] data-[highlighted=true]:outline-2 data-[highlighted=true]:outline-accent"
    >
      {point.canArchive ? <NoteMore pointId={point.id} writes={writes} /> : null}
      <p className="m-0">{point.text}</p>
      {point.rootCause ? (
        <p className="m-0 text-[length:var(--text-xs)] text-ink-muted">
          <b>Root cause:</b> {point.rootCause}
        </p>
      ) : null}
      {showTheme ? <NoteThemeLine point={point} writes={writes} /> : null}
      {showItem || showAuthor ? (
        <NoteMeta point={point} showItem={showItem} showAuthor={showAuthor} />
      ) : null}
      <NoteDecision point={point} scope={scope} writes={writes} />
    </article>
  );
}

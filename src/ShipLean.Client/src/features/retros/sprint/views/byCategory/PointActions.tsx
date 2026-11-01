import { useEffect, useRef, useState } from 'react';
import type { RetroPointDto, RetroThemeListDto } from '../../../../../api/retros';
import { ThemePicker } from '../../../../retroShared/ThemePicker';
import { retrosTestIds as ids } from '../../../testIds';
import type { useRetroPointWrites } from './useRetroPointWrites';

const buttonClass =
  'h-5 cursor-pointer rounded-[var(--radius-xs)] border border-dashed border-edge bg-transparent px-1.5 text-[length:var(--text-2xs)] whitespace-nowrap text-ink-muted hover:border-solid hover:text-ink disabled:cursor-default disabled:opacity-60';

/**
 * The buttons on a point. The theme can always move; + Action item shows on a Can be improved point with no action
 * item yet; Archive shows only where the server says `canArchive`. Nothing here edits the point's text.
 */
export function PointActions({
  point,
  themes,
  writes,
}: {
  point: RetroPointDto;
  themes: RetroThemeListDto | undefined;
  writes: ReturnType<typeof useRetroPointWrites>;
}) {
  const [picking, setPicking] = useState(false);
  const { moveTheme, addAction, archive } = writes;
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
    <>
      {point.category === 'improve' && point.action === null ? (
        <button
          type="button"
          data-testid={ids.retroPointAddAction}
          className={buttonClass}
          disabled={addAction.isPending}
          onClick={() => addAction.mutate(point.id)}
        >
          + Action item
        </button>
      ) : null}
      <span ref={anchor} className="relative inline-block">
        <button
          type="button"
          data-testid={ids.retroPointMoveTheme}
          aria-expanded={picking}
          className={buttonClass}
          disabled={themes === undefined}
          onClick={() => setPicking(!picking)}
        >
          Move theme
        </button>
        {picking && themes ? (
          <span className="absolute right-0 z-10 mt-1 block">
            <ThemePicker
              themes={themes.themes}
              mode={themes.mode}
              value={point.theme?.id ?? null}
              onPick={(pick) => {
                setPicking(false);
                moveTheme.mutate({ pointId: point.id, pick });
              }}
            />
          </span>
        ) : null}
      </span>
      {point.canArchive ? (
        <button
          type="button"
          data-testid={ids.retroPointArchive}
          className={buttonClass}
          disabled={archive.isPending}
          onClick={() => archive.mutate(point.id)}
        >
          Archive
        </button>
      ) : null}
    </>
  );
}

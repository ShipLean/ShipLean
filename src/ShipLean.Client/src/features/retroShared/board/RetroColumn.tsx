import type { RetroCategoryColumnDto, RetroPointDto } from '@shiplean/core/api/retros';
import type { RetroActionScope } from '../actionDialog/RetroActionScope';
import { retroCategoryColours, retroCategoryLabels } from '../retroCategories';
import { RetroNote } from '../note/RetroNote';
import { retroSharedTestIds as ids } from '../testIds';
import { AddNote } from './AddNote';
import type { AddTarget } from './AddTarget';
import type { NoteOptions } from './NoteOptions';
import { ThemeGroupHeading } from './ThemeGroupHeading';

/** One category's column: title, the number of notes shown, how many still need a decision, the notes, then Add. */
export function RetroColumn({
  column,
  groupByTheme,
  filter,
  note,
  add,
  scope,
  adding,
  onOpenAdd,
  onCloseAdd,
  onAdded,
}: {
  column: RetroCategoryColumnDto;
  groupByTheme: boolean;
  filter: (point: RetroPointDto) => boolean;
  note: NoteOptions;
  add: AddTarget;
  scope: RetroActionScope;
  adding: boolean;
  onOpenAdd: () => void;
  onCloseAdd: () => void;
  onAdded?: (point: RetroPointDto) => void;
}) {
  const groups = column.groups
    .map((group) => ({ ...group, points: group.points.filter(filter) }))
    .filter((group) => group.points.length > 0);
  const shown = groups.reduce((sum, group) => sum + group.points.length, 0);
  const colour = retroCategoryColours[column.category];
  const renderNote = (point: RetroPointDto, showTheme: boolean) => (
    <RetroNote
      key={point.id}
      point={point}
      showItem={note.showItem}
      showAuthor={note.showAuthor}
      showTheme={showTheme && note.showTheme !== false}
      highlighted={point.id === note.highlightedPointId}
      scope={scope}
    />
  );
  return (
    <section
      data-testid={ids.retroBoardColumn}
      data-category={column.category}
      aria-label={retroCategoryLabels[column.category]}
      className="grid min-w-0 grid-cols-[minmax(0,1fr)] content-start gap-2 rounded-md border border-edge border-t-[3px] p-2"
      style={{
        ['--cat' as string]: colour,
        borderTopColor: colour,
        background: `color-mix(in srgb, ${colour} 4%, var(--color-canvas))`,
      }}
    >
      <header className="flex items-center gap-1.5 px-0.5 font-semibold text-ink">
        <span>{retroCategoryLabels[column.category]}</span>
        <span className="font-normal text-ink-muted" data-count="shown">
          {shown}
        </span>
        <span className="flex-1" />
        {column.toDecide > 0 ? (
          <span
            data-to-decide={column.toDecide}
            className="text-[length:var(--text-xs)] text-caution"
          >
            {column.toDecide} to decide
          </span>
        ) : null}
      </header>
      {groupByTheme
        ? groups.map((group) => (
            <div
              key={group.theme?.id ?? 'unthemed'}
              className="grid grid-cols-[minmax(0,1fr)] gap-2"
            >
              <ThemeGroupHeading theme={group.theme} recurring={group.recurring} />
              {group.points.map((point) => renderNote(point, false))}
            </div>
          ))
        : groups.flatMap((group) => group.points).map((point) => renderNote(point, true))}
      <AddNote
        category={column.category}
        add={add}
        open={adding}
        onOpen={onOpenAdd}
        onClose={onCloseAdd}
        onAdded={onAdded}
      />
    </section>
  );
}

import { useState } from 'react';
import { Avatar } from '../../../components/Avatar';
import { CapacityBar } from '../../../components/CapacityBar';
import { sprintsTestIds as ids } from '../testIds';
import { CapacityDaysSheet } from './CapacityDaysSheet';
import type { CapacityLine } from './CapacityRow';
import { effectiveDaysOf, type CapacityShape, type DraftRow } from './capacityDraft';

interface CapacityPeopleProps {
  lines: CapacityLine[];
  shape: CapacityShape;
  total: number;
  readOnly: boolean;
  sprintName: string;
  /** What planning uses while the table is empty. */
  velocity: number;
  /** The velocity the points are worked from; undefined while the box holds no valid number. */
  typedVelocity: number | undefined;
  /** Whether a member's row differs from what the server holds. */
  unsaved: (userId: string) => boolean;
  onChange: (userId: string, row: DraftRow) => void;
  onRemove: (userId: string) => void;
}

const muted = 'text-ink-muted';

/**
 * The people on a phone (SHIP-1808 P2, P6): a People header with the team total, then a two-line row per person (name and
 * points; a bar of days available out of the working days and the days in words; the note under them). A row opens the
 * days sheet. The desktop keeps CapacityTable.
 */
export function CapacityPeople({
  lines,
  shape,
  total,
  readOnly,
  sprintName,
  velocity,
  typedVelocity,
  unsaved,
  onChange,
  onRemove,
}: CapacityPeopleProps) {
  const [openId, setOpenId] = useState<string | null>(null);
  const empty = lines.length === 0;
  const open = lines.find((line) => line.user.id === openId) ?? null;
  return (
    <div data-testid={ids.capacityTable} className="border-t border-edge">
      <div
        data-testid={ids.capacityPeopleHead}
        className="flex items-baseline justify-between gap-2 border-b border-edge bg-sunken px-3 py-2.5"
      >
        <span className="font-semibold">
          People <span className={`ml-1 font-normal ${muted}`}>{lines.length}</span>
        </span>
        <span className={muted}>
          Team capacity{' '}
          <span data-testid={ids.capacityTotal} className="font-semibold text-ink tabular-nums">
            {empty ? '—' : total}
          </span>{' '}
          <span className="font-semibold text-ink">pts</span>
        </span>
      </div>
      {lines.map((line) => (
        <PersonRow
          key={line.user.id}
          line={line}
          shape={shape}
          readOnly={readOnly}
          unsaved={unsaved(line.user.id)}
          onOpen={() => setOpenId(line.user.id)}
        />
      ))}
      <p
        data-testid={empty ? ids.capacityEmpty : undefined}
        className={`m-0 px-3 py-2.5 text-[length:var(--text-sm)] ${muted}`}
      >
        {empty
          ? `No capacity entered: planning uses velocity ${velocity}`
          : 'Capacity is the velocity shared out by days available. Only the team total is compared with what the sprint has committed.'}
      </p>
      <CapacityDaysSheet
        line={open}
        shape={shape}
        sprintName={sprintName}
        velocity={typedVelocity}
        people={lines.length}
        onChange={(row) => open && onChange(open.user.id, row)}
        onRemove={() => open && onRemove(open.user.id)}
        onClose={() => setOpenId(null)}
      />
    </div>
  );
}

interface PersonRowProps {
  line: CapacityLine;
  shape: CapacityShape;
  readOnly: boolean;
  unsaved: boolean;
  onOpen: () => void;
}

/** Avatar; name and points; the days bar and "9 / 10 days · default"; the note. An ended sprint's row opens nothing. */
function PersonRow({ line, shape, readOnly, unsaved, onOpen }: PersonRowProps) {
  const { user, row, points } = line;
  const isDefault = row.days === '';
  // An ended sprint's default is the calendar of when it was stored, so it shows as "default" without a number.
  const days = readOnly && isDefault ? undefined : effectiveDaysOf(row, shape);
  const body = (
    <>
      <span className="pt-0.5">
        <Avatar {...user} size="lg" />
      </span>
      <span className="grid min-w-0 grid-cols-[minmax(0,1fr)_7.75rem] items-center gap-x-3 gap-y-1">
        <span className="flex min-w-0 items-center gap-2">
          <span className="truncate font-medium text-ink">{user.name}</span>
          {unsaved ? (
            <span
              data-testid={ids.capacityUnsaved}
              className="flex flex-none items-center gap-1 text-[length:var(--text-xs)] text-ink-muted"
            >
              <i aria-hidden="true" className="size-1.5 rounded-full bg-caution" />
              Unsaved
            </span>
          ) : null}
        </span>
        <span className="text-right tabular-nums">
          <span data-testid={ids.capacityPoints} className="font-semibold text-ink">
            {points ?? '—'}
          </span>
          <span className={`ml-1 ${muted}`}>pts</span>
        </span>
        <CapacityBar used={days ?? 0} capacity={shape.workingDays} />
        <span
          data-testid={ids.capacityRowDays}
          className={`text-right text-[length:var(--text-sm)] whitespace-nowrap tabular-nums ${muted}`}
        >
          {days === undefined
            ? isDefault
              ? 'default'
              : `${row.days} days`
            : `${days} / ${shape.workingDays} days${isDefault ? ' · default' : ''}`}
        </span>
        {row.note === '' ? null : (
          <span
            data-testid={ids.capacityRowNote}
            className={`col-span-2 truncate text-[length:var(--text-sm)] ${muted}`}
          >
            {row.note}
          </span>
        )}
      </span>
    </>
  );
  const layout =
    'grid w-full grid-cols-[auto_minmax(0,1fr)_auto] items-start gap-x-3 border-b border-edge px-3 py-2.5 text-left';
  if (readOnly) {
    return (
      <div data-testid={ids.capacityRow} className={layout}>
        {body}
        <span />
      </div>
    );
  }
  return (
    <button
      type="button"
      data-testid={ids.capacityRow}
      className={`${layout} min-h-14 active:bg-signal-soft`}
      onClick={onOpen}
    >
      {body}
      <span aria-hidden="true" className={`self-center text-[length:var(--text-md)] ${muted}`}>
        ›
      </span>
    </button>
  );
}

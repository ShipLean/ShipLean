import { Fragment, type ReactNode } from 'react';
import { Sheet } from './Sheet';
import { touchTestIds } from './testIds';

/**
 * One row of an `ActionSheet` (K1). A row with none of the optional anatomy fields renders as a plain text row.
 * - `icon`: a leading glyph in a fixed 20px slot (`TypeIcon`, `StateDot`, `Avatar` or an svg).
 * - `hint`: a muted sub-line under the label. `detail`: a right-aligned muted value.
 * - `checked`: ✓ at the right, the row tinted, `aria-current="true"`. `trailing: 'chevron'`: › for a row that opens another sheet.
 * - `group`: consecutive actions with the same group sit under one heading; a change of group draws a divider. An empty
 *   group draws the divider without a heading.
 * - `toggle`: the row is a switch; tapping runs `onSelect` and keeps the sheet open. Toggle rows render after all the
 *   others, so give a toggle a group of its own: one that shares a group with plain rows draws that heading again at the end.
 */
export type SheetAction = {
  id: string;
  label: string;
  onSelect: () => void;
  tone?: 'danger';
  disabled?: boolean;
  icon?: ReactNode;
  hint?: string;
  detail?: ReactNode;
  checked?: boolean;
  trailing?: 'chevron';
  group?: string;
  toggle?: { on: boolean };
};

/** The part of a row its face draws: what `ActionSheetRowFace` takes. */
export type SheetRowFace = Pick<
  SheetAction,
  'label' | 'icon' | 'hint' | 'detail' | 'checked' | 'trailing'
>;

/** What a sheet's own rows look like; a row that is not an action (a checkbox, a status line) takes it too. */
export const actionSheetRowClass =
  'flex min-h-[var(--touch-target)] w-full items-center rounded-control px-2 py-1.5 text-left text-[length:var(--text-base)] active:bg-signal-soft disabled:cursor-default disabled:text-ink-muted disabled:active:bg-transparent';

/** The tint of a checked (current) row. */
export const actionSheetCheckedClass = 'bg-signal-soft';

/** The content a sheet shows before its action rows: nodes, or a function given the sheet's close. */
export type SheetSlot = ReactNode | ((close: () => void) => ReactNode);

// A field counts as given unless it is undefined or null (a null detail draws nothing, as a null icon does).
function given(value: unknown): boolean {
  return value !== undefined && value !== null;
}

function hasAnatomy(face: SheetRowFace, iconSlot: boolean): boolean {
  return (
    iconSlot ||
    given(face.hint) ||
    given(face.detail) ||
    face.checked === true ||
    face.trailing !== undefined
  );
}

/**
 * What a row shows inside its box: the icon slot, the label with its hint, the detail, ✓ and ›. A row with none of them
 * is its bare label. `iconSlot` keeps the 20px slot for a row without an icon, so labels line up in a sheet where some
 * rows have one.
 */
export function ActionSheetRowFace({
  face,
  iconSlot = given(face.icon),
}: {
  face: SheetRowFace;
  iconSlot?: boolean;
}) {
  if (!hasAnatomy(face, iconSlot)) return <>{face.label}</>;
  return (
    <>
      {iconSlot ? (
        <span aria-hidden="true" className="inline-grid size-5 flex-none place-items-center">
          {face.icon}
        </span>
      ) : null}
      <span className="grid min-w-0 flex-1">
        <span className="truncate">{face.label}</span>
        {given(face.hint) ? (
          <span
            data-testid={touchTestIds.actionSheetHint}
            className="text-[length:var(--text-sm)] text-ink-muted"
          >
            {face.hint}
          </span>
        ) : null}
      </span>
      {given(face.detail) ? (
        <span
          data-testid={touchTestIds.actionSheetDetail}
          className="flex-none text-[length:var(--text-sm)] whitespace-nowrap text-ink-muted"
        >
          {face.detail}
        </span>
      ) : null}
      {face.checked ? (
        <span aria-hidden="true" className="flex-none text-signal">
          ✓
        </span>
      ) : null}
      {face.trailing === 'chevron' ? (
        <span aria-hidden="true" className="flex-none text-ink-muted">
          ›
        </span>
      ) : null}
    </>
  );
}

/** A group's heading, after a divider unless it is the sheet's first group. An empty label draws only the divider. */
export function ActionSheetGroupHeading({ label, divider }: { label: string; divider: boolean }) {
  return (
    <>
      {divider ? (
        <hr aria-hidden="true" className="-mx-3 my-1 border-0 border-t border-edge" />
      ) : null}
      {label !== '' ? (
        <div
          data-testid={touchTestIds.actionSheetGroup}
          className="px-2 pt-2 pb-0.5 text-[length:var(--text-xs)] font-semibold tracking-wide text-ink-muted uppercase"
        >
          {label}
        </div>
      ) : null}
    </>
  );
}

/** The footnote after a sheet's rows, over a divider. */
export function ActionSheetFootnote({ children }: { children: ReactNode }) {
  return (
    <div
      data-testid={touchTestIds.actionSheetFooter}
      className="-mx-3 mt-1 border-t border-edge px-5 pt-2 text-[length:var(--text-sm)] text-ink-muted"
    >
      {children}
    </div>
  );
}

/** One action row: running it closes the sheet. */
export function ActionSheetRow({
  action,
  onClose,
  iconSlot,
}: {
  action: SheetAction;
  onClose: () => void;
  // Keep the icon slot when the row has no icon (set by ActionSheet when any of its rows has one).
  iconSlot?: boolean;
}) {
  const slot = iconSlot ?? given(action.icon);
  const anatomy = hasAnatomy(action, slot);
  return (
    <button
      type="button"
      data-testid={touchTestIds.actionSheetRow}
      data-action-id={action.id}
      disabled={action.disabled}
      aria-current={action.checked ? 'true' : undefined}
      className={`${actionSheetRowClass} ${action.tone === 'danger' ? 'text-alert' : 'text-ink'}${anatomy ? ' gap-3' : ''}${action.checked ? ` ${actionSheetCheckedClass}` : ''}`}
      onClick={() => {
        action.onSelect();
        onClose();
      }}
    >
      <ActionSheetRowFace face={action} iconSlot={slot} />
    </button>
  );
}

/** A switch row: tapping it runs the action and keeps the sheet open. */
function ActionSheetToggle({
  action,
  on,
  iconSlot,
}: {
  action: SheetAction;
  on: boolean;
  iconSlot: boolean;
}) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={on}
      data-testid={touchTestIds.actionSheetToggle}
      data-action-id={action.id}
      disabled={action.disabled}
      className={`${actionSheetRowClass} gap-3 text-ink`}
      onClick={() => action.onSelect()}
    >
      <ActionSheetRowFace face={{ ...action, checked: false }} iconSlot={iconSlot} />
      <span
        aria-hidden="true"
        className={`relative h-5 w-9 flex-none rounded-full transition-colors ${on ? 'bg-signal' : 'bg-edge'}`}
      >
        <span
          className={`absolute top-0.5 size-4 rounded-full bg-panel shadow-sm transition-[left] ${on ? 'left-[18px]' : 'left-0.5'}`}
        />
      </span>
    </button>
  );
}

/** The rows of an `ActionSheet`: groups with their headings, the plain rows, then the toggles, then the footnote. */
function ActionSheetRows({
  actions,
  onClose,
  footer,
}: {
  actions: readonly SheetAction[];
  onClose: () => void;
  footer?: ReactNode;
}) {
  const ordered = [
    ...actions.filter((action) => !action.toggle),
    ...actions.filter((action) => action.toggle),
  ];
  const iconSlot = ordered.some((action) => given(action.icon));
  return (
    <>
      {ordered.map((action, index) => {
        const previous = ordered[index - 1];
        const startsGroup =
          action.group !== undefined && (index === 0 || previous?.group !== action.group);
        const leavesGroup = previous?.group !== undefined && action.group === undefined;
        return (
          <Fragment key={action.id}>
            {startsGroup ? (
              <ActionSheetGroupHeading label={action.group ?? ''} divider={index > 0} />
            ) : null}
            {leavesGroup ? <ActionSheetGroupHeading label="" divider /> : null}
            {action.toggle ? (
              <ActionSheetToggle action={action} on={action.toggle.on} iconSlot={iconSlot} />
            ) : (
              <ActionSheetRow action={action} onClose={onClose} iconSlot={iconSlot} />
            )}
          </Fragment>
        );
      })}
      {given(footer) ? (
        <ActionSheetFootnote>{footer}</ActionSheetFootnote>
      ) : null}
    </>
  );
}

/**
 * A `Sheet` of action rows (K5, K1), with an optional `children` slot before them and a `footer` footnote after them.
 * Picking a row runs it, then closes the sheet; a toggle row runs and stays open.
 */
export function ActionSheet({
  open,
  title,
  subtitle,
  actions,
  onClose,
  footer,
  children,
}: {
  open: boolean;
  title?: string;
  subtitle?: string;
  actions: readonly SheetAction[];
  onClose: () => void;
  footer?: ReactNode;
  children?: SheetSlot;
}) {
  return (
    <Sheet open={open} title={title ?? ''} subtitle={subtitle} onClose={onClose}>
      {typeof children === 'function' ? children(onClose) : children}
      <ActionSheetRows actions={actions} onClose={onClose} footer={footer} />
    </Sheet>
  );
}

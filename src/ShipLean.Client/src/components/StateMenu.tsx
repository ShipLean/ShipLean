import {
  Fragment,
  useEffect,
  useId,
  useRef,
  useState,
  type KeyboardEvent,
  type ReactNode,
} from 'react';
import {
  actionSheetCheckedClass,
  ActionSheetFootnote,
  ActionSheetGroupHeading,
  actionSheetRowClass,
  ActionSheetRowFace,
} from '../touch/ActionSheet';
import { useTouchShell } from '../touch/useTouchShell';
import { StateDot, type StateCategory } from './StateDot';
import { componentTestIds } from './testIds';

export interface StateOption {
  id: string;
  name: string;
  category: StateCategory;
}

const categoryOrder: StateCategory[] = [
  'proposed',
  'inProgress',
  'resolved',
  'completed',
  'removed',
];

function byCategory(states: StateOption[]): StateOption[] {
  return categoryOrder.flatMap((category) => states.filter((state) => state.category === category));
}

const categoryLabels: Record<StateCategory, string> = {
  proposed: 'Proposed',
  inProgress: 'In progress',
  resolved: 'Resolved',
  completed: 'Completed',
  removed: 'Removed',
};

/** The footnote under the states at touch, unless the caller passes its own `footer` (SHIP-714 D4). */
export const stateMenuFootnote =
  'States for each type are set per project in Settings. The colour follows the category.';

/**
 * A list of a type's states. It takes focus when it mounts. Arrow keys move the highlight, Enter picks it, a click picks
 * any; Esc and Tab out call `onClose`. At 900px and below (inside the caller's sheet) it is drawn with the action sheet's
 * anatomy (K1b): states under their category headings, ✓ and a tint on the current one, `subtitle` above and `footer`
 * (the settings footnote by default) after. `title` names the list (default "State"). Above 900px the menu is the
 * compact dropdown list; `subtitle` and `footer` are touch only.
 */
export function StateMenu({
  states,
  currentId,
  onSelect,
  onClose,
  title = 'State',
  subtitle,
  footer,
}: {
  states: StateOption[];
  currentId?: string;
  onSelect: (stateId: string) => void;
  onClose: () => void;
  title?: string;
  subtitle?: string;
  footer?: ReactNode;
}) {
  const { touch } = useTouchShell();
  // At touch the states sit under their category headings, so the list (and the arrow keys) go category by category.
  const shown = touch ? byCategory(states) : states;
  const list = useRef<HTMLUListElement>(null);
  const sheetList = useRef<HTMLDivElement>(null);
  const prefix = useId();
  useEffect(() => (list.current ?? sheetList.current)?.focus(), []);
  const [active, setActive] = useState(() =>
    Math.max(
      0,
      shown.findIndex((s) => s.id === currentId),
    ),
  );
  const last = shown.length - 1;

  const onKeyDown = (event: KeyboardEvent) => {
    if (event.key === 'Escape') {
      event.stopPropagation();
      onClose();
    } else if (event.key === 'Tab') {
      onClose();
    } else if (event.key === 'ArrowDown') {
      event.preventDefault();
      setActive((i) => Math.min(last, i + 1));
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      setActive((i) => Math.max(0, i - 1));
    } else if (event.key === 'Home') {
      event.preventDefault();
      setActive(0);
    } else if (event.key === 'End') {
      event.preventDefault();
      setActive(last);
    } else if (event.key === 'Enter' || event.key === ' ') {
      const state = shown[active];
      if (state) {
        event.preventDefault();
        onSelect(state.id);
      }
    }
  };

  const listProps = {
    'data-testid': componentTestIds.stateMenu,
    role: 'listbox',
    'aria-label': title,
    tabIndex: 0,
    'aria-activedescendant': shown[active] ? `${prefix}-${shown[active].id}` : undefined,
    onKeyDown,
  } as const;

  if (touch) {
    return (
      <div ref={sheetList} {...listProps} className="grid gap-1 outline-none">
        {subtitle ? (
          <p className="m-0 px-2 text-[length:var(--text-sm)] text-ink-muted">{subtitle}</p>
        ) : null}
        {shown.map((state, index) => {
          const current = state.id === currentId;
          return (
            <Fragment key={state.id}>
              {index === 0 || shown[index - 1]?.category !== state.category ? (
                <ActionSheetGroupHeading label={categoryLabels[state.category]} divider={false} />
              ) : null}
              <div
                id={`${prefix}-${state.id}`}
                data-testid={componentTestIds.stateMenuOption}
                data-category={state.category}
                data-active={index === active}
                role="option"
                aria-selected={current}
                aria-current={current ? 'true' : undefined}
                className={`${actionSheetRowClass} cursor-pointer gap-3 text-ink ${current ? actionSheetCheckedClass : ''}`}
                onClick={() => onSelect(state.id)}
              >
                <ActionSheetRowFace
                  face={{
                    label: state.name,
                    icon: <StateDot category={state.category} />,
                    checked: current,
                  }}
                />
              </div>
            </Fragment>
          );
        })}
        <ActionSheetFootnote>{footer ?? stateMenuFootnote}</ActionSheetFootnote>
      </div>
    );
  }

  return (
    <ul
      ref={list}
      {...listProps}
      className="grid min-w-44 gap-px rounded-panel border border-edge bg-panel p-1 shadow-panel focus-visible:outline-2 focus-visible:outline-signal"
    >
      {states.map((state, index) => (
        <li
          key={state.id}
          id={`${prefix}-${state.id}`}
          data-testid={componentTestIds.stateMenuOption}
          data-category={state.category}
          data-active={index === active}
          role="option"
          aria-selected={state.id === currentId}
          className={`flex h-[30px] cursor-pointer items-center justify-between gap-3 rounded-control px-2 text-[length:var(--text-sm)] text-ink hover:bg-signal-soft ${
            index === active ? 'bg-signal-soft' : ''
          } ${state.id === currentId ? 'font-semibold' : ''}`}
          onClick={() => onSelect(state.id)}
        >
          <StateDot category={state.category} name={state.name} />
          <span className="text-[length:var(--text-xs)] text-ink-muted">
            {categoryLabels[state.category]}
          </span>
        </li>
      ))}
    </ul>
  );
}

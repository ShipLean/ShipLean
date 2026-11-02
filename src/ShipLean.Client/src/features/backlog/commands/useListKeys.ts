import { useEffect, useRef } from 'react';
import { backlogTestIds as ids } from '../testIds';
import { useSelection } from '../selection/useSelection';
import { useListCommand, useRunListCommand } from './listCommands';

const typing = 'input:not([type="checkbox"]), select, textarea, [contenteditable]';
const rowSelector = `[data-testid="${ids.row}"]`;

/** Whether a key press is the list's: focus is on a row (or inside one), not in a field, not in a dialog. */
function belongsToList(event: KeyboardEvent): boolean {
  if (event.defaultPrevented || event.isComposing) return false;
  // Ctrl, Cmd and Alt are other shortcuts' (Ctrl+P, Alt+Up and Alt+Down moving the row).
  if (event.ctrlKey || event.metaKey || event.altKey) return false;
  const target = event.target;
  if (!(target instanceof Element)) return false;
  if (target.closest(typing) !== null || target.closest('[role="dialog"]') !== null) return false;
  return target.closest(rowSelector) !== null;
}

function focusRow(id: string) {
  document.querySelector<HTMLElement>(`${rowSelector}[data-row-id="${CSS.escape(id)}"]`)?.focus();
}

/**
 * The list's keys, live only while a row has focus: J and K move it (the commands `focusNext` and `focusPrev`, which
 * this registers), the rest run the commands other features register, by name. `M` waits for one more key (a sprint
 * number, T or B) and Esc cancels it. Mount it once per screen, inside the selection provider.
 */
export function useListKeys(): void {
  const selection = useSelection();
  const run = useRunListCommand();
  const latest = useRef(selection);
  useEffect(() => {
    latest.current = selection;
  });

  const move = (step: 1 | -1) => {
    const { orderedIds, focusedId } = latest.current;
    const at = focusedId === null ? -1 : orderedIds.indexOf(focusedId);
    const next = at === -1 ? orderedIds[0] : orderedIds[at + step];
    if (next === undefined) return;
    latest.current.setFocused(next);
    focusRow(next);
  };
  useListCommand('focusNext', () => move(1));
  useListCommand('focusPrev', () => move(-1));

  // Rows leave on a refetch, a search or a move. The focused row's neighbour takes the focus, in the DOM too when the
  // list had it. Focus survives a refetch by id on its own, since the row keeps its key.
  const lastOrder = useRef<readonly string[]>(selection.orderedIds);
  const { orderedIds, focusedId, setFocused } = selection;
  useEffect(() => {
    const before = lastOrder.current;
    lastOrder.current = orderedIds;
    if (focusedId === null || orderedIds.includes(focusedId)) return;
    const gone = before.indexOf(focusedId);
    const successor = before.slice(gone + 1).find((id) => orderedIds.includes(id));
    const next =
      successor ??
      [...before.slice(0, Math.max(gone, 0))].reverse().find((id) => orderedIds.includes(id));
    setFocused(next ?? null);
    const active = document.activeElement;
    if (next !== undefined && (active === null || active === document.body)) {
      requestAnimationFrame(() => focusRow(next));
    }
  }, [orderedIds, focusedId, setFocused]);

  const chord = useRef(false);
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (!belongsToList(event)) {
        chord.current = false;
        return;
      }
      const key = event.key.length === 1 ? event.key.toLowerCase() : event.key;
      if (key === 'Shift') return;
      const handled = (): void => event.preventDefault();

      if (chord.current) {
        chord.current = false;
        handled();
        if (/^[1-9tb]$/.test(key) && !event.shiftKey) run('moveMenu', key);
        return;
      }
      if (key === 'Escape') {
        handled();
        run('clear');
      } else if (key === 'Enter') {
        // Enter on a control inside the row (the title link, a button) is that control's own.
        if (event.target instanceof Element && event.target.matches(rowSelector)) {
          handled();
          run('open');
        }
      } else if (key === '?') {
        handled();
        run('help');
      } else if (!event.shiftKey) {
        if (key === 'j') run('focusNext');
        else if (key === 'k') run('focusPrev');
        else if (key === 'x') run('toggleSelect');
        else if (key === 'n') run('addBelow');
        else if (key === '/') run('search');
        else if (key === 'm') chord.current = true;
        else if (/^[0-9]$/.test(key)) run('estimate', key);
        else return;
        handled();
      }
    };
    document.addEventListener('keydown', onKeyDown);
    return () => document.removeEventListener('keydown', onKeyDown);
  }, [run]);
}

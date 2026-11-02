import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, test } from 'vitest';
import { SelectionProvider } from '../selection/SelectionProvider';
import { ListCommandsProvider, useListCommand } from './listCommands';
import { useListKeys } from './useListKeys';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const calls: string[] = [];

function Keys() {
  useListKeys();
  useListCommand('open', () => void calls.push('open'));
  useListCommand('toggleSelect', () => void calls.push('toggleSelect'));
  useListCommand('estimate', (arg) => void calls.push(`estimate:${String(arg)}`));
  useListCommand('moveMenu', (arg) => void calls.push(`moveMenu:${String(arg)}`));
  useListCommand('clear', () => void calls.push('clear'));
  return (
    <table>
      <tbody>
        <tr data-testid="backlog-row" data-row-id="a" tabIndex={0}>
          <td>
            <input type="checkbox" aria-label="box" />
            <button type="button">inside</button>
            <input type="text" aria-label="points" />
            <span contentEditable suppressContentEditableWarning>
              edit
            </span>
          </td>
        </tr>
      </tbody>
    </table>
  );
}

let root: Root;
let host: HTMLDivElement;
beforeEach(() => {
  calls.length = 0;
  host = document.createElement('div');
  document.body.append(host);
  root = createRoot(host);
  act(() => {
    root.render(
      <ListCommandsProvider>
        <SelectionProvider>
          <Keys />
        </SelectionProvider>
      </ListCommandsProvider>,
    );
  });
});
afterEach(() => {
  act(() => root.unmount());
  host.remove();
});

function press(target: Element | null, init: KeyboardEventInit) {
  const event = new KeyboardEvent('keydown', { bubbles: true, cancelable: true, ...init });
  act(() => void target?.dispatchEvent(event));
  return event;
}
const at = (selector: string) => host.querySelector(selector);

test('Key_WhenOnTheRow_ShouldRunItsCommand', () => {
  const event = press(at('tr'), { key: 'x' });
  press(at('tr'), { key: '7' });
  press(at('tr'), { key: 'Enter' });
  expect(calls).toEqual(['toggleSelect', 'estimate:7', 'open']);
  expect(event.defaultPrevented).toBe(true);
});

test('Key_WhenOnTheRowsCheckbox_ShouldStillRun', () => {
  press(at('input[type=checkbox]'), { key: 'x' });
  expect(calls).toEqual(['toggleSelect']);
});

test('Enter_WhenOnAControlInTheRow_ShouldLeaveItToTheControl', () => {
  const event = press(at('button'), { key: 'Enter' });
  expect(calls).toEqual([]);
  expect(event.defaultPrevented).toBe(false);
});

test('Key_WhenInAFieldOrEditable_ShouldDoNothing', () => {
  press(at('input[type=text]'), { key: 'x' });
  press(at('[contenteditable]'), { key: '5' });
  expect(calls).toEqual([]);
});

test('Key_WhenModifiedOrShifted_ShouldDoNothing', () => {
  for (const init of [{ ctrlKey: true }, { metaKey: true }, { altKey: true }, { shiftKey: true }]) {
    press(at('tr'), { key: 'x', ...init });
  }
  expect(calls).toEqual([]);
});

test('Key_WhenOutsideTheList_ShouldDoNothing', () => {
  press(document.body, { key: 'x' });
  expect(calls).toEqual([]);
});

test('M_WhenFollowedByASprintNumberTOrB_ShouldRunMoveMenuWithIt', () => {
  for (const key of ['m', '1', 'm', '9', 'm', 't', 'm', 'b']) press(at('tr'), { key });
  expect(calls).toEqual(['moveMenu:1', 'moveMenu:9', 'moveMenu:t', 'moveMenu:b']);
});

test('M_WhenFollowedByZero_ShouldRunNothingAndEndTheChord', () => {
  press(at('tr'), { key: 'm' });
  press(at('tr'), { key: '0' });
  press(at('tr'), { key: '4' });
  expect(calls).toEqual(['estimate:4']);
});

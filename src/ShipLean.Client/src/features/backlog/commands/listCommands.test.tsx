import { act, useEffect } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, test } from 'vitest';
import {
  ListCommandsProvider,
  useListCommand,
  useRunListCommand,
  type ListCommandName,
} from './listCommands';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const calls: string[] = [];
let run: (name: ListCommandName, arg?: unknown) => void = () => undefined;

function Owner({ label }: { label: string }) {
  useListCommand('focusNext', (arg) => void calls.push(`${label}:${String(arg)}`));
  return null;
}

function Runner() {
  const runCommand = useRunListCommand();
  useEffect(() => {
    run = runCommand;
  });
  return null;
}

let root: Root;
const mount = (owners: string[]) =>
  act(() => {
    root.render(
      <ListCommandsProvider>
        <Runner />
        {owners.map((label) => (
          <Owner key={label} label={label} />
        ))}
      </ListCommandsProvider>,
    );
  });

beforeEach(() => {
  calls.length = 0;
  root = createRoot(document.createElement('div'));
});
afterEach(() => act(() => root.unmount()));

test('the_last_registration_wins_and_unregisters', () => {
  mount(['first', 'second']);
  act(() => run('focusNext', 1));
  expect(calls).toEqual(['second:1']);

  mount(['first']);
  act(() => run('focusNext', 2));
  expect(calls).toEqual(['second:1', 'first:2']);

  mount([]);
  act(() => run('focusNext', 3));
  expect(calls).toEqual(['second:1', 'first:2']);
});

test('running_an_unregistered_command_does_nothing', () => {
  mount(['first']);

  expect(() => act(() => run('help'))).not.toThrow();
  expect(calls).toEqual([]);
});

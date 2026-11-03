import { useState, type ReactNode } from 'react';
import {
  ListCommandsProvider,
  useRunListCommand,
  type ListCommandName,
} from '../commands/listCommands';

const NAMES: readonly ListCommandName[] = [
  'focusNext',
  'focusPrev',
  'toggleSelect',
  'clear',
  'open',
  'estimate',
  'moveMenu',
  'addBelow',
  'search',
  'help',
];

function Runner() {
  const run = useRunListCommand();
  const [arg, setArg] = useState('');
  return (
    <div>
      <input
        data-testid="command-arg"
        aria-label="Command argument"
        value={arg}
        onChange={(event) => setArg(event.target.value)}
      />
      {NAMES.map((name) => (
        <button
          key={name}
          type="button"
          data-testid={`run-command-${name}`}
          onClick={() => run(name, arg === '' ? undefined : arg)}
        >
          {name}
        </button>
      ))}
    </div>
  );
}

// CT-only: runs a list command by name. Wrap what registers the command in it, then click `run-command-<name>`; the
// text in `command-arg` goes with the command (nothing when empty). Tests reach a command this way, never by a key.
export function CommandHarness({ children }: { children: ReactNode }) {
  return (
    <ListCommandsProvider>
      <Runner />
      {children}
    </ListCommandsProvider>
  );
}

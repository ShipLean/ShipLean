import { useState } from 'react';
import { useListCommand } from '../commands/listCommands';

// CT-only: registers `toggleSelect` and shows what it was last run with, to prove CommandHarness reaches a command.
export function CommandHarnessProbe() {
  const [last, setLast] = useState('none');
  useListCommand('toggleSelect', (arg) => setLast(`ran:${String(arg)}`));
  return <span data-testid="command-probe">{last}</span>;
}

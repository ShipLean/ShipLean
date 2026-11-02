import { backlogTestIds as ids } from '../testIds';
import { useRunListCommand } from './listCommands';

// The toolbar button that shows the keyboard shortcuts: it runs the `help` command, as the ? key does.
export function KeysButton() {
  const run = useRunListCommand();
  return (
    <button
      type="button"
      data-testid={ids.keysButton}
      className="btn-ghost"
      aria-label="Keyboard shortcuts"
      aria-keyshortcuts="?"
      onClick={() => run('help')}
    >
      ?
    </button>
  );
}

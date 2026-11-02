import { backlogTestIds as ids } from '../testIds';
import { useRunListCommand } from './listCommands';

// The toolbar button that shows the keyboard shortcuts: it runs the `help` command, as the ? key does.
export function KeysButton() {
  const run = useRunListCommand();
  return (
    <button
      type="button"
      data-testid={ids.keysButton}
      className="btn-ghost gap-1.5 border-transparent bg-transparent"
      title="Keyboard shortcuts"
      aria-keyshortcuts="?"
      onClick={() => run('help')}
    >
      Keys
      <kbd className="inline-grid h-[18px] min-w-[18px] place-items-center rounded-xs border border-b-2 border-edge bg-panel px-1 font-mono text-[length:var(--text-2xs)] font-semibold text-ink">
        ?
      </kbd>
    </button>
  );
}

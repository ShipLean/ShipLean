import { showToast } from '../../../components/toastStore';
import { agentsTestIds as ids } from '../testIds';

/** The line that connects Claude Code. On a desktop it scrolls sideways; at 900px and below it wraps, with Copy. */
export function CopyAddLine({ line }: { line: string }) {
  const copy = async () => {
    try {
      await navigator.clipboard.writeText(line);
      showToast('Copied the Claude Code line', { tone: 'ok' });
    } catch {
      showToast("Couldn't copy the line. Select it and copy it yourself.", { tone: 'danger' });
    }
  };
  return (
    <>
      <code
        data-testid={ids.connectedAddLine}
        className="block overflow-x-auto rounded-control border border-edge bg-panel px-2 py-1 whitespace-nowrap text-ink touch:overflow-x-visible touch:break-all touch:whitespace-normal"
      >
        {line}
      </code>
      <button
        type="button"
        data-testid={ids.connectedCopy}
        className="btn-ghost hidden touch:inline-flex touch:h-[var(--touch-target)] touch:self-start"
        onClick={() => void copy()}
      >
        Copy
      </button>
    </>
  );
}

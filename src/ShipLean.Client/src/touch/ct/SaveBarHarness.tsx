import { useState } from 'react';
import { Dialog } from '../../components/Dialog';
import { SaveBar } from '../SaveBar';
import { saveBarHarnessIds as ids } from './saveBarHarnessIds';

/** CT-only: a long page (or a full-screen dialog's body) ending in a SaveBar, with counters for its handlers. */
export function SaveBarHarness({
  dirty = true,
  saveDisabled,
  saving,
  messageIsButton,
  inDialog,
}: {
  dirty?: boolean;
  saveDisabled?: boolean;
  saving?: boolean;
  messageIsButton?: boolean;
  inDialog?: boolean;
}) {
  const [saves, setSaves] = useState(0);
  const [discards, setDiscards] = useState(0);
  const [messageClicks, setMessageClicks] = useState(0);

  const message = messageIsButton ? (
    <button
      type="button"
      data-testid={ids.ctSaveBarMessageButton}
      className="text-signal underline"
      onClick={() => setMessageClicks((count) => count + 1)}
    >
      3 unsaved changes
    </button>
  ) : (
    'Unsaved changes to the Fields pane and two other panes of this board'
  );

  const content = (
    <>
      <p>
        Saves <span data-testid={ids.ctSaveBarSaves}>{saves}</span> · Discards{' '}
        <span data-testid={ids.ctSaveBarDiscards}>{discards}</span> · Message{' '}
        <span data-testid={ids.ctSaveBarMessageClicks}>{messageClicks}</span>
      </p>
      {Array.from({ length: 40 }, (_, row) => (
        <p key={row} className="m-0 border-b border-edge py-2">
          Row {row + 1}
        </p>
      ))}
      <p data-testid={ids.ctSaveBarPageEnd} className="m-0 py-2">
        Page end
      </p>
      <SaveBar
        dirty={dirty}
        message={message}
        onDiscard={() => setDiscards((count) => count + 1)}
        onSave={() => setSaves((count) => count + 1)}
        saveDisabled={saveDisabled}
        saving={saving}
      />
    </>
  );

  if (inDialog) {
    return (
      <Dialog title="Taskboard settings" onClose={() => undefined}>
        {content}
      </Dialog>
    );
  }
  return <div className="flex flex-col p-4">{content}</div>;
}

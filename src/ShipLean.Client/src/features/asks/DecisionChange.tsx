import type { DecisionEditorProps } from './regions';
import { asksTestIds } from './testIds';

// TODO: the real change form (options, own answer, note, save, settled warning) replaces this stub; it keeps the root test id.
export function DecisionChange({ onClose }: DecisionEditorProps) {
  return (
    <div data-testid={asksTestIds.decisionChange}>
      <button type="button" data-testid={asksTestIds.changeCancel} onClick={onClose}>
        Cancel
      </button>
    </div>
  );
}

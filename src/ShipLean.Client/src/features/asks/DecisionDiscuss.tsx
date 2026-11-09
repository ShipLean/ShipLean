import type { DecisionEditorProps } from './regions';
import { asksTestIds } from './testIds';

// TODO: the real discuss composer replaces this stub; it keeps the root test id.
export function DecisionDiscuss({ onClose }: DecisionEditorProps) {
  return (
    <div data-testid={asksTestIds.decisionDiscuss}>
      <button type="button" data-testid={asksTestIds.discussCancel} onClick={onClose}>
        Cancel
      </button>
    </div>
  );
}

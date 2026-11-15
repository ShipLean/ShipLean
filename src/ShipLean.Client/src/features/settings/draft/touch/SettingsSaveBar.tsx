import { useState } from 'react';
import { SaveBar } from '../../../../touch/SaveBar';
import { settingsTestIds } from '../../testIds';
import type { Change } from '../model';
import { ChangesSheet } from './ChangesSheet';

/**
 * The one Save on a phone (SHIP-805 rule 15): the header's Save and Discard move into the K5 SaveBar while the draft
 * has changes. Its message opens the list of changes, each row jumping to its tab as the desktop list does.
 */
export function SettingsSaveBar({
  changes,
  saving,
  onJump,
  onSave,
  onDiscard,
}: {
  changes: Change[];
  saving: boolean;
  onJump(change: Change): void;
  onSave(): void;
  onDiscard(): void;
}) {
  const [listOpen, setListOpen] = useState(false);
  const count = changes.length;
  return (
    <>
      <SaveBar
        dirty={count > 0}
        saving={saving}
        onSave={onSave}
        onDiscard={() => {
          setListOpen(false);
          onDiscard();
        }}
        message={
          <button
            type="button"
            data-testid={settingsTestIds.unsaved}
            aria-haspopup="dialog"
            className="font-semibold text-ink"
            onClick={() => setListOpen(true)}
          >
            {count} unsaved {count === 1 ? 'change' : 'changes'}
          </button>
        }
      />
      <ChangesSheet
        open={listOpen && count > 0}
        changes={changes}
        onJump={onJump}
        onClose={() => setListOpen(false)}
      />
    </>
  );
}

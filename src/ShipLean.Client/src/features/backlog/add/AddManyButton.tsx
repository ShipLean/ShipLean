import { useContext } from 'react';
import { backlogTestIds as ids } from '../testIds';
import { PasteContext } from './usePasteIntake';

// The toolbar button that opens the paste review on an empty box.
export function AddManyButton() {
  const open = useContext(PasteContext);
  if (open === null) return null;
  return (
    <button
      type="button"
      data-testid={ids.addMany}
      className="btn-ghost"
      onClick={() => open('', 'story')}
    >
      Add many
    </button>
  );
}

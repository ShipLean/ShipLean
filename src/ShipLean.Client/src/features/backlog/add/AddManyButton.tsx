import { useContext, type ReactNode } from 'react';
import type { SheetAction } from '../../../touch/ActionSheet';
import { pasteIcon } from '../toolbar/moreIcons';
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
      className="btn-ghost touch:h-auto touch:min-h-[var(--touch-target)] touch:w-full touch:justify-start touch:border-0 touch:bg-transparent touch:px-2 touch:py-1.5 touch:text-[length:var(--text-base)] touch:font-normal touch:text-ink"
      onClick={() => open('', 'story')}
    >
      Add many
    </button>
  );
}

// Add many as a K1 action row for ⋯ More at touch; null where no paste host is mounted. Renders `children` with it.
export function AddManyAction({
  children,
}: {
  children: (action: SheetAction | null) => ReactNode;
}) {
  const open = useContext(PasteContext);
  return children(
    open === null
      ? null
      : {
          id: 'add-many',
          label: 'Add many…',
          icon: pasteIcon,
          hint: 'Paste a list of titles, one per line',
          onSelect: () => open('', 'story'),
        },
  );
}

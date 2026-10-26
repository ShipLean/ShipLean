import type { HTMLAttributes, Ref } from 'react';
import { backlogTestIds as ids } from '../testIds';

// The ⠿ handle of a row. Disabled (and hidden) while a filter is on.
export function Grip({
  itemKey,
  disabled,
  buttonRef,
  ...handlers
}: HTMLAttributes<HTMLButtonElement> & {
  itemKey: string;
  disabled: boolean;
  buttonRef: Ref<HTMLButtonElement>;
}) {
  return (
    <button
      type="button"
      ref={buttonRef}
      data-testid={ids.grip}
      aria-label={`Move ${itemKey}`}
      disabled={disabled}
      tabIndex={disabled ? -1 : 0}
      className={`cursor-grab touch-none text-ink-muted ${disabled ? 'invisible' : ''}`}
      {...handlers}
    >
      ⠿
    </button>
  );
}

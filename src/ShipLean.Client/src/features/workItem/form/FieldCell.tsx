import type { ReactNode } from 'react';
import { workItemTestIds as ids } from '../testIds';

// One cell of the fields strip: the label (with an accent dot while the field has an unsaved change) over its control.
export function FieldCell({
  label,
  field,
  changed,
  htmlFor,
  className = '',
  children,
}: {
  label: string;
  field: string;
  changed: boolean;
  htmlFor?: string;
  className?: string;
  children: ReactNode;
}) {
  return (
    <div data-field={field} className={`grid content-start gap-1 ${className}`}>
      <label
        htmlFor={htmlFor}
        className="flex items-center gap-1 text-[length:var(--text-xs)] font-semibold tracking-wide text-ink-muted uppercase"
      >
        {label}
        {changed ? (
          <span
            data-testid={ids.changedDot}
            role="img"
            aria-label="Unsaved change"
            className="size-1.5 rounded-full bg-signal"
          />
        ) : null}
      </label>
      {children}
    </div>
  );
}

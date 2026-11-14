import type { ReactNode } from 'react';
import { FieldCell } from '../../workItem/form/FieldCell';

// A strip cell that only shows a value (Started, the stories-and-bugs count).
export function ReadOnlyCell({
  label,
  field,
  testId,
  className,
  children,
}: {
  label: string;
  field: string;
  testId: string;
  className?: string;
  children: ReactNode;
}) {
  return (
    <FieldCell label={label} field={field} changed={false} className={className}>
      <span
        data-testid={testId}
        className="inline-flex h-7 items-center text-[length:var(--text-sm)]"
      >
        {children}
      </span>
    </FieldCell>
  );
}

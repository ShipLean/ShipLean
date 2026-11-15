import type { ReactNode } from 'react';

/** A label and its control, in the profile panels' two-column form. */
export function FormRow({
  label,
  htmlFor,
  children,
}: {
  label: string;
  htmlFor?: string;
  children: ReactNode;
}) {
  return (
    <>
      <label
        htmlFor={htmlFor}
        className="border-b border-edge/60 px-3 pt-[15px] pb-2.5 text-[length:var(--text-sm)] font-medium text-ink-muted touch:border-b-0 touch:pt-3 touch:pb-1"
      >
        {label}
      </label>
      <div className="grid content-start justify-items-start gap-1.5 border-b border-edge/60 px-3 py-2.5 text-[length:var(--text-sm)] touch:justify-items-stretch touch:pt-0 touch:pb-3">
        {children}
      </div>
    </>
  );
}

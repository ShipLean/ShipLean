import { useState, type Ref } from 'react';
// One labelled row of a sign-in card. A password field gets a Show/Hide toggle.
export function FormField({
  id,
  label,
  type,
  value,
  onChange,
  autoComplete,
  autoFocus,
  testId,
  revealTestId,
  inputRef,
}: {
  id: string;
  label: string;
  type: 'email' | 'password';
  value: string;
  onChange: (value: string) => void;
  autoComplete: string;
  autoFocus?: boolean;
  testId: string;
  revealTestId?: string;
  inputRef?: Ref<HTMLInputElement>;
}) {
  const [shown, setShown] = useState(false);
  const input = (
    <input
      ref={inputRef}
      id={id}
      data-testid={testId}
      className={`field w-full ${type === 'password' && revealTestId ? 'pr-13' : ''}`}
      style={{ height: 32, fontSize: 'var(--text-base)' }}
      type={type === 'password' && shown ? 'text' : type}
      autoComplete={autoComplete}
      autoFocus={autoFocus}
      value={value}
      onChange={(event) => onChange(event.target.value)}
    />
  );
  return (
    <>
      <label htmlFor={id} className="text-[length:var(--text-sm)] text-ink-muted">
        {label}
      </label>
      {revealTestId ? (
        <div className="relative">
          {input}
          <button
            type="button"
            data-testid={revealTestId}
            aria-label={shown ? 'Hide password' : 'Show password'}
            className="absolute top-1 right-1 h-6 rounded-sm px-1.5 text-[length:var(--text-xs)] text-ink-muted hover:bg-sunken hover:text-ink"
            onClick={() => setShown(!shown)}
          >
            {shown ? 'Hide' : 'Show'}
          </button>
        </div>
      ) : (
        input
      )}
    </>
  );
}

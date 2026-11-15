import { useState, type Ref } from 'react';
import { useTouchShell } from '../../touch/useTouchShell';

// What a phone keyboard needs for an email address; harmless, and invisible, on a desktop.
const emailKeyboard = {
  inputMode: 'email',
  autoCapitalize: 'off',
  autoCorrect: 'off',
  spellCheck: false,
} as const;

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
  enterKeyHint,
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
  enterKeyHint?: 'next' | 'go';
}) {
  const [shown, setShown] = useState(false);
  const { touch } = useTouchShell();
  const input = (
    <input
      ref={inputRef}
      id={id}
      data-testid={testId}
      className={`field w-full ${type === 'password' && revealTestId ? 'pr-13' : ''}`}
      style={{ height: touch ? 40 : 32, fontSize: 'var(--text-base)' }}
      type={type === 'password' && shown ? 'text' : type}
      autoComplete={autoComplete}
      autoFocus={autoFocus}
      enterKeyHint={enterKeyHint}
      {...(type === 'email' ? emailKeyboard : {})}
      value={value}
      onChange={(event) => onChange(event.target.value)}
    />
  );
  const label14 = touch ? 'text-[length:var(--text-md)]' : 'text-[length:var(--text-sm)]';
  const row = (
    <>
      <label htmlFor={id} className={`${label14} text-ink-muted`}>
        {label}
      </label>
      {revealTestId ? (
        <div className="relative">
          {input}
          <button
            type="button"
            data-testid={revealTestId}
            aria-label={shown ? 'Hide password' : 'Show password'}
            className="absolute top-1 right-1 h-6 touch:h-8 rounded-sm px-1.5 touch:px-2.5 text-[length:var(--text-xs)] text-ink-muted hover:bg-sunken hover:text-ink"
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
  // On a phone the label sits above its field, so each pair is one cell of a single column.
  return touch ? <div className="grid gap-1">{row}</div> : row;
}

import { useEffect, useRef, useState } from 'react';
import { backlogTestIds as ids } from '../testIds';

export interface ColumnsMenuProps {
  options: readonly { id: string; label: string; required: boolean }[];
  // The ids of the columns the user hides.
  hidden: readonly string[];
  onChange: (hidden: string[]) => void;
}

// The Columns button and its checklist. Required columns (Key, Title) are listed but can't be hidden.
export function ColumnsMenu({ options, hidden, onChange }: ColumnsMenuProps) {
  const [open, setOpen] = useState(false);
  const wrapper = useRef<HTMLSpanElement>(null);
  useEffect(() => {
    if (!open) return undefined;
    const close = (event: MouseEvent | KeyboardEvent) => {
      if (
        event instanceof KeyboardEvent
          ? event.key === 'Escape'
          : !wrapper.current?.contains(event.target as Node)
      ) {
        setOpen(false);
      }
    };
    document.addEventListener('mousedown', close);
    document.addEventListener('keydown', close);
    return () => {
      document.removeEventListener('mousedown', close);
      document.removeEventListener('keydown', close);
    };
  }, [open]);
  return (
    <span ref={wrapper} className="relative">
      <button
        type="button"
        data-testid={ids.columnsButton}
        aria-expanded={open}
        className="btn-ghost"
        onClick={() => setOpen(!open)}
      >
        Columns
      </button>
      {open ? (
        <div
          data-testid={ids.columnsMenu}
          className="absolute top-[calc(100%+4px)] right-0 z-30 grid w-56 gap-1 rounded-panel border border-edge bg-panel p-[var(--space-2)] shadow-panel"
        >
          <span className="text-[length:var(--text-xs)] font-semibold tracking-wide text-ink-muted uppercase">
            Columns, just for you
          </span>
          {options.map((option) => (
            <label
              key={option.id}
              className="flex items-center gap-2 text-[length:var(--text-sm)] text-ink"
            >
              <input
                type="checkbox"
                data-testid={ids.columnOption}
                data-column={option.id}
                checked={!hidden.includes(option.id)}
                disabled={option.required}
                onChange={(event) =>
                  onChange(
                    event.target.checked
                      ? hidden.filter((id) => id !== option.id)
                      : [...hidden, option.id],
                  )
                }
              />
              {option.label}
              {option.required ? (
                <span className="text-[length:var(--text-xs)] text-ink-muted">always</span>
              ) : null}
            </label>
          ))}
        </div>
      ) : null}
    </span>
  );
}

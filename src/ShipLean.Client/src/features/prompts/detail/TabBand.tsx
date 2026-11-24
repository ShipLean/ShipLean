import { useRef, type KeyboardEvent, type ReactNode } from 'react';

export interface TabBandTab<T extends string> {
  id: T;
  label: string;
  count?: ReactNode;
  testId: string;
}

// The sunken full-width band of plain text tabs under a prompt's callouts (design/screens/prompts.html, .dtabs).
export function TabBand<T extends string>({
  tabs,
  active,
  onChange,
  aside,
  label,
}: {
  tabs: TabBandTab<T>[];
  active: T;
  onChange: (id: T) => void;
  aside?: ReactNode;
  label: string;
}) {
  const refs = useRef(new Map<T, HTMLButtonElement>());

  function onKeyDown(event: KeyboardEvent<HTMLButtonElement>, index: number) {
    if (event.key !== 'ArrowRight' && event.key !== 'ArrowLeft') return;
    event.preventDefault();
    const step = event.key === 'ArrowRight' ? 1 : -1;
    const next = tabs[(index + step + tabs.length) % tabs.length];
    onChange(next.id);
    refs.current.get(next.id)?.focus();
  }

  return (
    <div className="flex flex-wrap items-center gap-0.5 border-b border-edge bg-canvas px-3 py-2">
      <div role="tablist" aria-label={label} className="flex flex-wrap items-center gap-0.5">
        {tabs.map((tab, index) => {
          const selected = tab.id === active;
          return (
            <button
              key={tab.id}
              ref={(node) => {
                if (node) refs.current.set(tab.id, node);
                else refs.current.delete(tab.id);
              }}
              type="button"
              role="tab"
              aria-selected={selected}
              tabIndex={selected ? 0 : -1}
              data-testid={tab.testId}
              onClick={() => onChange(tab.id)}
              onKeyDown={(event) => onKeyDown(event, index)}
              className={`inline-flex h-(--control-height) items-center rounded-control px-2.5 text-[length:var(--text-sm)] hover:text-ink focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-signal touch:min-h-(--touch-target) ${
                selected ? 'text-ink' : 'text-ink-muted'
              }`}
            >
              {tab.label}
              {tab.count !== undefined && tab.count !== null && (
                <span className="ml-1 text-[length:var(--text-2xs)] text-ink-muted">
                  {tab.count}
                </span>
              )}
            </button>
          );
        })}
      </div>
      {aside !== undefined && (
        <div className="ml-auto touch:ml-0 text-[length:var(--text-sm)] text-ink-muted">
          {aside}
        </div>
      )}
    </div>
  );
}

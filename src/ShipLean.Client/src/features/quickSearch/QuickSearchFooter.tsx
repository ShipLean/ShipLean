import { quickSearchTestIds as ids } from './testIds';

function KeyCap({ children }: { children: string }) {
  return (
    <kbd className="rounded-xs border border-edge bg-panel px-1 font-mono text-[length:var(--text-2xs)] text-ink">
      {children}
    </kbd>
  );
}

export function QuickSearchFooter({ note }: { note: string | null }) {
  return (
    <footer
      data-testid={ids.footer}
      // At touch widths the key caps are hidden, so a footer without a note has nothing to show.
      className={`flex items-center gap-3 border-t border-edge bg-canvas px-3 py-2 text-[length:var(--text-2xs)] text-ink-muted ${note === null ? 'touch:hidden' : ''}`}
    >
      <span className="flex items-center gap-3 touch:hidden">
        <span className="flex items-center gap-1">
          <KeyCap>↑</KeyCap>
          <KeyCap>↓</KeyCap> move
        </span>
        <span className="flex items-center gap-1">
          <KeyCap>↵</KeyCap> open
        </span>
        <span className="flex items-center gap-1">
          <KeyCap>Esc</KeyCap> close
        </span>
      </span>
      {note === null ? null : (
        <span data-testid={ids.note} className="ml-auto">
          {note}
        </span>
      )}
    </footer>
  );
}

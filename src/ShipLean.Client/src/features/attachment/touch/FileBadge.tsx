// The "MD" badge before a Markdown file's name (attachment.png): a small tinted tile.
export function FileBadge({ large = false }: { large?: boolean }) {
  return (
    <span
      aria-hidden="true"
      className={`inline-grid flex-none place-items-center rounded-control border border-edge bg-signal-soft font-[family-name:var(--font-mono)] text-ink-muted ${large ? 'h-9 min-w-12 text-[length:var(--text-base)]' : 'h-6 min-w-8 text-[length:var(--text-xs)]'}`}
    >
      MD
    </span>
  );
}

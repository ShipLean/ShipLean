export function ActionRow({
  glyph,
  label,
  detail,
}: {
  glyph: string;
  label: string;
  detail: string | null;
}) {
  return (
    <>
      <span aria-hidden="true" className="text-center text-ink-muted">
        {glyph}
      </span>
      <span className="min-w-0 truncate">{label}</span>
      <span className="text-right text-[length:var(--text-xs)] text-ink-muted">{detail}</span>
    </>
  );
}

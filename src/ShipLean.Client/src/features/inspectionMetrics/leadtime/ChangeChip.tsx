/** A mean's change against the period before (lower is better): ≈ same under 3 %, else ▼/▲ and the percent. */
export function ChangeChip({ pct, unit }: { pct: number | null; unit: string }) {
  if (pct === null) return null;
  const base =
    'inline-flex h-5 items-center gap-1 rounded-full px-2 text-[length:var(--text-xs)] font-semibold whitespace-nowrap';
  if (Math.abs(pct) < 3) return <span className={`${base} bg-sunken`}>≈ same</span>;
  const better = pct < 0;
  return (
    <span
      className={base}
      title={`Against the ${unit} before`}
      style={{
        background: better
          ? 'color-mix(in srgb, var(--color-ok) 18%, transparent)'
          : 'color-mix(in srgb, var(--color-danger) 16%, transparent)',
      }}
    >
      {better ? '▼' : '▲'} {Math.abs(pct)}%
    </span>
  );
}

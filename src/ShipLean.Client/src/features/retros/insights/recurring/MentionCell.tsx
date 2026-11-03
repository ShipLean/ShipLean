/** One column's (sprint or week) mention count, shaded by how many there are against the busiest cell in the table. */
export function MentionCell({
  count,
  max,
  columnKey,
  columnLabel,
}: {
  count: number;
  max: number;
  columnKey: string;
  columnLabel: string;
}) {
  const strength = max > 0 ? Math.round((count / max) * 70) : 0;
  return (
    <td
      data-cell="mentions"
      data-sprint-id={columnKey}
      data-count={count}
      title={`${columnLabel}: ${count} mention${count === 1 ? '' : 's'}`}
      className="px-1 py-1 text-center text-[length:var(--text-sm)] tabular-nums"
    >
      <span
        className="inline-block min-w-7 rounded-[var(--radius-xs)] px-1 py-0.5"
        style={{
          background:
            count > 0
              ? `color-mix(in srgb, var(--color-warn) ${strength}%, transparent)`
              : undefined,
        }}
      >
        {count}
      </span>
    </td>
  );
}

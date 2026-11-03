import { retrosTestIds as ids } from '../../testIds';

/** One theme's mentions that belong to no sprint: hatched and set apart, since they are in Total but not in any column's trend. */
export function NoSprintCell({ count }: { count: number }) {
  return (
    <td
      data-testid={ids.retroNosprintCell}
      data-cell="no-sprint"
      data-count={count}
      title={`No sprint: ${count} mention${count === 1 ? '' : 's'}`}
      className={`border-l-2 border-edge px-1 py-1 text-center text-[length:var(--text-sm)] tabular-nums ${count === 0 ? 'text-ink-muted opacity-45' : ''}`}
      style={{
        backgroundImage:
          'repeating-linear-gradient(135deg, transparent 0 5px, color-mix(in srgb, var(--color-border) 45%, transparent) 5px 6px)',
      }}
    >
      {count}
    </td>
  );
}

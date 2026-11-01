import { Avatar } from '../../../../components/Avatar';
import { Chip } from '../../../../components/Chip';
import { TypeIcon } from '../../../../components/TypeIcon';
import type { RetroPointDto } from '../../../../api/retros';
import { retroCategoryColours, retroCategoryLabels } from '../../../retroShared/retroCategories';
import { retrosTestIds as ids } from '../../testIds';
import { Highlight } from './Highlight';

/** One point of the result: category, text with root cause, theme, work item, sprint and author. Clicking it opens its retro. */
export function SearchRow({
  point,
  q,
  logOnly,
  selected,
  onSelect,
}: {
  point: RetroPointDto;
  q: string | undefined;
  /** The row matched only through its retro's raw failure log. */
  logOnly: boolean;
  selected: boolean;
  onSelect: () => void;
}) {
  const { author, workItem, theme, sprint } = point;
  const cell = 'border-b border-edge px-2.5 py-1.5 align-middle';
  return (
    <tr
      data-testid={ids.retrosRow}
      data-point-id={point.id}
      data-retro-id={point.retroId}
      aria-selected={selected}
      tabIndex={0}
      className={`cursor-pointer ${selected ? 'bg-signal-soft' : 'hover:bg-sunken'}`}
      onClick={onSelect}
      onKeyDown={(event) => {
        if (event.key === 'Enter') onSelect();
      }}
    >
      <td className={cell}>
        <span className="inline-flex items-center gap-1.5 text-[length:var(--text-sm)] whitespace-nowrap">
          <span
            aria-hidden="true"
            className="size-2 flex-none rounded-[2px]"
            style={{ background: retroCategoryColours[point.category] }}
          />
          {retroCategoryLabels[point.category]}
        </span>
      </td>
      <td className={`${cell} break-words`}>
        <Highlight text={point.text} q={q} />
        {point.rootCause ? (
          <div className="text-[length:var(--text-xs)] text-ink-muted">
            Root cause: <Highlight text={point.rootCause} q={q} />
          </div>
        ) : null}
        {logOnly ? (
          <div className="text-[length:var(--text-xs)] text-ink-muted">
            Match in the raw failure log
          </div>
        ) : null}
      </td>
      <td className={`${cell} break-words`}>
        {theme ? (
          <span className="text-[length:var(--text-sm)]">
            <Highlight text={theme.name} q={q} />
          </span>
        ) : (
          <span className="text-[length:var(--text-sm)] text-ink-muted italic">Unthemed</span>
        )}
        {point.recurring ? (
          <>
            {' '}
            <span title="Recurring">
              <Chip tone="warn">↻</Chip>
            </span>
          </>
        ) : null}
      </td>
      <td className={`${cell} overflow-hidden`}>
        {workItem ? (
          <span className="flex min-w-0 items-center gap-1.5">
            <TypeIcon type={workItem.type.id} />
            <span className="font-mono text-[length:var(--text-xs)]">{workItem.key}</span>
            <span className="truncate text-[length:var(--text-sm)]">{workItem.title}</span>
          </span>
        ) : (
          <span className="text-[length:var(--text-sm)] text-ink-muted">Sprint</span>
        )}
      </td>
      <td className={`${cell} text-[length:var(--text-sm)]`}>{sprint?.name}</td>
      <td
        className={cell}
        title={author.agentName ? `${author.user.name} · ${author.agentName}` : author.user.name}
      >
        <Avatar {...author.user} />
      </td>
    </tr>
  );
}

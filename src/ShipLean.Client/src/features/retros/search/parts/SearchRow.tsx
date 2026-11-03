import { Avatar } from '../../../../components/Avatar';
import { Chip } from '../../../../components/Chip';
import { TypeIcon } from '../../../../components/TypeIcon';
import type { RetroPointDto } from '../../../../api/retros';
import { retroCategoryColours, retroCategoryLabels } from '../../../retroShared/retroCategories';
import { retrosTestIds as ids } from '../../testIds';
import { Highlight } from './Highlight';
import { matchesRootCause } from './matches';

/** One point of the result on one line (the hover title carries the root cause only when the search matched it): category, text (the root cause too when the search matched it), theme, work item key, sprint and author. The rest is on hover and in the selected retro. Clicking it opens its retro. */
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
  const oneLine = 'overflow-hidden text-ellipsis whitespace-nowrap';
  const showRootCause = matchesRootCause(point.rootCause, q);
  const fullText =
    showRootCause && point.rootCause ? `${point.text}\nRoot cause: ${point.rootCause}` : point.text;
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
      <td className={`${cell} ${oneLine}`} title={fullText}>
        <Highlight text={point.text} q={q} />
        {showRootCause && point.rootCause ? (
          <span className="text-ink-muted">
            {' · Root cause: '}
            <Highlight text={point.rootCause} q={q} />
          </span>
        ) : null}
        {logOnly ? <span className="text-ink-muted"> · Match in the raw failure log</span> : null}
      </td>
      <td className={`${cell} ${oneLine}`} title={theme?.name}>
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
      <td className={`${cell} ${oneLine}`} title={workItem?.title}>
        {workItem ? (
          <span className="flex min-w-0 items-center gap-1.5">
            <TypeIcon type={workItem.type.id} />
            <span className="font-mono whitespace-nowrap text-[length:var(--text-xs)]">
              {workItem.key}
            </span>
          </span>
        ) : (
          <span className="text-[length:var(--text-sm)] text-ink-muted">Sprint</span>
        )}
      </td>
      <td className={`${cell} ${oneLine} text-[length:var(--text-sm)]`}>{sprint?.name}</td>
      <td
        className={cell}
        title={author.agentName ? `${author.user.name} · ${author.agentName}` : author.user.name}
      >
        <Avatar {...author.user} />
      </td>
    </tr>
  );
}

import type { MouseEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import type { RetroFeatureRowDto } from '@shiplean/core/api/retros';
import { RETRO_CATEGORIES } from '@shiplean/core/api/retros';
import { StateDot } from '../../../components/StateDot';
import { TypeIcon } from '../../../components/TypeIcon';
import { routePath } from '../../../routes';
import { useTouchShell } from '../../../touch/useTouchShell';
import { retrosTestIds as ids } from '../testIds';
import { CategoryCount } from './CategoryCount';
import { DecidedProgress } from './DecidedProgress';

const cell =
  'border-t border-edge px-2.5 py-[7px] align-middle text-[length:var(--text-sm)] touch:h-12 touch:whitespace-nowrap';
const num = `${cell} text-right tabular-nums`;
// The Feature cell wraps (key and title, then the chip) and stays pinned at the left of the table's box.
const featureCell = `${cell} w-full min-w-48 touch:sticky touch:left-0 touch:z-[1] touch:min-w-[200px] touch:max-w-60 touch:bg-panel touch:whitespace-normal touch:shadow-[1px_0_0_var(--color-edge)]`;

/** One Feature (or loose story or bug): its counts per category, what is left to decide and the last retro; the title opens the item's retros, and at 900px and below the whole row does. */
export function FeatureRow({ projectKey, row }: { projectKey: string; row: RetroFeatureRowDto }) {
  const { item, totals } = row;
  const { touch } = useTouchShell();
  const navigate = useNavigate();
  const to = routePath('retroItem', { key: projectKey, itemKey: item.key });
  // The title's own link has already handled its click; the rest of the row opens the item on touch.
  const openRow = (event: MouseEvent) => {
    if (touch && !event.defaultPrevented) navigate(to);
  };
  return (
    <tr
      data-testid={ids.retroFeaturesRow}
      data-key={item.key}
      className="hover:bg-sunken touch:cursor-pointer"
      onClick={openRow}
    >
      <td data-testid={ids.retroFeaturesFeatureCell} className={featureCell}>
        <div className="flex items-center gap-2 touch:flex-wrap touch:gap-y-0.5">
          <TypeIcon type={item.type.id} />
          <span className="font-mono text-[length:var(--text-xs)] whitespace-nowrap text-ink-muted">
            {item.key}
          </span>
          <Link
            data-testid={ids.retroFeaturesLink}
            to={to}
            className="font-medium text-ink hover:underline"
          >
            {item.title}
          </Link>
          {row.noFeatureRetro ? (
            <span
              data-testid={ids.retroFeaturesNoFeatureChip}
              className="rounded-full bg-sunken px-2 text-[length:var(--text-xs)] whitespace-nowrap"
            >
              No Feature retro yet
            </span>
          ) : null}
        </div>
      </td>
      <td className={cell}>
        <StateDot category={row.state.category} name={row.state.name} />
      </td>
      <td className={num}>{row.retros}</td>
      {RETRO_CATEGORIES.map((category) => (
        <td key={category} className={num}>
          <CategoryCount category={category} count={totals.counts[category]} />
        </td>
      ))}
      <td className={num}>
        {totals.toDecide ? (
          <span className="inline-flex h-[18px] items-center rounded-full bg-caution/20 px-1.5 text-[length:var(--text-2xs)] font-semibold">
            {totals.toDecide}
          </span>
        ) : (
          <span className="text-ink-muted">0</span>
        )}
      </td>
      <td className={cell}>
        <DecidedProgress totals={totals} />
      </td>
      <td className={num}>{totals.actions || <span className="text-ink-muted">0</span>}</td>
      <td className={`${cell} text-[length:var(--text-xs)] whitespace-nowrap text-ink-muted`}>
        {row.lastRetroLabel}
      </td>
    </tr>
  );
}

import { Link } from 'react-router-dom';
import type { RetroFeatureRowDto } from '../../../api/retros';
import { RETRO_CATEGORIES } from '../../../api/retros';
import { StateDot } from '../../../components/StateDot';
import { TypeIcon } from '../../../components/TypeIcon';
import { routePath } from '../../../routes';
import { retrosTestIds as ids } from '../testIds';
import { CategoryCount } from './CategoryCount';
import { DecidedProgress } from './DecidedProgress';

const cell = 'border-t border-edge px-2.5 py-[7px] align-middle text-[length:var(--text-sm)]';
const num = `${cell} text-right tabular-nums`;

/** One Feature (or loose story or bug): its counts per category, what is left to decide and the last retro; the title opens the item's retros. */
export function FeatureRow({ projectKey, row }: { projectKey: string; row: RetroFeatureRowDto }) {
  const { item, totals } = row;
  return (
    <tr data-testid={ids.retroFeaturesRow} data-key={item.key} className="hover:bg-sunken">
      <td className={`${cell} w-full min-w-48`}>
        <div className="flex items-center gap-2">
          <TypeIcon type={item.type.id} />
          <span className="font-mono text-[length:var(--text-xs)] whitespace-nowrap text-ink-muted">
            {item.key}
          </span>
          <Link
            to={routePath('retroItem', { key: projectKey, itemKey: item.key })}
            className="font-medium text-ink hover:underline"
          >
            {item.title}
          </Link>
          {row.noFeatureRetro ? (
            <span
              title="Only its stories and bugs have retros so far."
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

import type { RetroFeatureRowDto } from '@shiplean/core/api/retros';
import { RETRO_CATEGORIES } from '@shiplean/core/api/retros';
import { retrosTestIds as ids } from '../testIds';
import { CategoryHead } from './CategoryHead';
import { FeatureRow } from './FeatureRow';

const head =
  'bg-sunken px-2.5 py-1.5 text-left text-[length:var(--text-xs)] font-semibold whitespace-nowrap text-ink-muted touch:align-bottom touch:leading-tight touch:whitespace-normal';
// The Feature column stays put while the rest scrolls inside the table's box (SHIP-805 rules 1 and 7).
const pinnedHead = `${head} touch:sticky touch:left-0 touch:z-[1] touch:min-w-[200px] touch:max-w-60 touch:shadow-[1px_0_0_var(--color-edge)]`;
const headNum = `${head} text-right`;
const COLUMNS = RETRO_CATEGORIES.length + 7;

/** The table: a head in the fixed category order, one row per Feature, then the stories and bugs with no Feature; "Nothing here." when no row is left. */
export function FeaturesTable({
  projectKey,
  features,
  loose,
}: {
  projectKey: string;
  features: RetroFeatureRowDto[];
  loose: RetroFeatureRowDto[];
}) {
  return (
    <div
      data-testid={ids.retroFeaturesTableBox}
      className="overflow-x-auto rounded-panel border border-edge bg-panel"
    >
      <table className="w-full border-collapse">
        <thead>
          <tr>
            <th className={pinnedHead}>Feature</th>
            <th className={head}>State</th>
            <th className={headNum}>Retros</th>
            {RETRO_CATEGORIES.map((category) => (
              <th key={category} className={headNum}>
                <CategoryHead category={category} />
              </th>
            ))}
            <th className={headNum}>To decide</th>
            <th className={head}>Decided</th>
            <th className={headNum}>Actions</th>
            <th className={head}>Last retro</th>
          </tr>
        </thead>
        <tbody>
          {features.map((row) => (
            <FeatureRow key={row.item.id} projectKey={projectKey} row={row} />
          ))}
          {loose.length ? (
            <tr>
              <td
                data-testid={ids.retroFeaturesLoose}
                colSpan={COLUMNS}
                className="border-t border-edge bg-canvas px-2.5 py-1.5 text-[length:var(--text-xs)] font-semibold text-ink-muted"
              >
                <span className="touch:sticky touch:left-2">Stories and bugs with no Feature</span>
              </td>
            </tr>
          ) : null}
          {loose.map((row) => (
            <FeatureRow key={row.item.id} projectKey={projectKey} row={row} />
          ))}
          {features.length + loose.length === 0 ? (
            <tr>
              <td colSpan={COLUMNS} className="h-20 text-center text-ink-muted">
                Nothing here.
              </td>
            </tr>
          ) : null}
        </tbody>
      </table>
    </div>
  );
}

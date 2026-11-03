import type { RetroFeatureRowDto } from '../../../api/retros';
import type { RetroFeaturesAddress } from './retroFeaturesAddress';

function matches(row: RetroFeatureRowDto, { show, q }: RetroFeaturesAddress): boolean {
  const needle = q.trim().toLowerCase();
  if (needle && !`${row.item.key} ${row.item.title}`.toLowerCase().includes(needle)) return false;
  if (show === 'open') return row.totals.toDecide > 0;
  if (show === 'waiting') return row.noFeatureRetro;
  return true;
}

/** The rows the address leaves in view: To decide keeps rows with points to decide, No Feature retro yet only Features without their own retro. */
export function rowsToShow(
  rows: RetroFeatureRowDto[],
  address: RetroFeaturesAddress,
): RetroFeatureRowDto[] {
  return rows.filter((row) => matches(row, address));
}

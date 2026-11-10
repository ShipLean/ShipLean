// What the Features tab answers to a question about the Features on screen. Read-only, shown as a sheet, never spoken.
import type { FeatureRowDto } from '@shiplean/core/api/featuresOverview';
import type { AnswerSheetContent } from '../voice/contracts';
import { rollupText } from './list/filters';

export type FeatureAskTopic = 'blocked' | 'atRisk' | 'howFar';

const byRank = (rows: FeatureRowDto[]) => [...rows].sort((a, b) => a.rank - b.rank);

const keys = (rows: FeatureRowDto[]) => rows.map((r) => r.key).join(', ');

const progress = (row: FeatureRowDto) => rollupText(row.rollups) || 'nothing under it yet';

// `rows` are the Features on screen; `only` narrows the answer to one Feature (the focused or the named one).
export function answerFor(
  topic: FeatureAskTopic,
  rows: FeatureRowDto[],
  only: FeatureRowDto | null,
): AnswerSheetContent {
  const ordered = byRank(only === null ? rows : [only]);
  switch (topic) {
    case 'blocked':
    case 'atRisk': {
      const health = topic === 'blocked' ? 'blocked' : 'atRisk';
      const label = topic === 'blocked' ? 'blocked' : 'at risk';
      const found = ordered.filter((r) => r.health === health);
      return {
        lead:
          found.length === 0 ? `Nothing is ${label}.` : `${found.length} ${label}: ${keys(found)}`,
        rows: found.map((r) => ({
          key: r.key,
          title: r.title,
          counts: r.statusLine ?? progress(r),
        })),
      };
    }
    case 'howFar': {
      // A named Feature answers with its progress whatever its state; the list answers with the open ones.
      const open =
        only !== null ? ordered : ordered.filter((r) => r.state.category !== 'completed');
      return {
        lead:
          open.length === 0
            ? 'Nothing is open here.'
            : `${open.length} open ${open.length === 1 ? 'Feature' : 'Features'}`,
        rows: open.map((r) => ({ key: r.key, title: r.title, counts: progress(r) })),
      };
    }
  }
}

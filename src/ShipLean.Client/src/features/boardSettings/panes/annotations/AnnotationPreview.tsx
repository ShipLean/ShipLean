import type { BoardLevel } from '../../../../api/boards';
import type { CardAnnotationsJson } from '../../../../api/persisted';
import { TypeIcon } from '../../../../components/TypeIcon';
import { boardSettingsTestIds as ids } from '../../testIds';
import { annotationRowsFor, hasEffect } from './annotationRows';

const sample = {
  stories: { title: 'Show delivery estimate in the basket', type: 'story', counts: '2/5 tasks' },
  features: {
    title: 'Saved payment methods',
    type: 'feature',
    counts: '3/5 stories · 1/2 bugs · 1/2 features',
  },
  epics: {
    title: 'Checkout rebuild',
    type: 'epic',
    counts: '2/4 features · 7/10 stories · 2/3 bugs',
  },
  taskboard: { title: 'Show delivery estimate in the basket', type: 'story', counts: '2/5 tasks' },
} as const;

/** An example card of the board: its roll-ups show only for the annotations the draft has on. */
export function AnnotationPreview({
  level,
  annotations,
}: {
  level: BoardLevel;
  annotations: CardAnnotationsJson;
}) {
  const card = sample[level];
  const shown = annotationRowsFor(level).filter(
    (row) => annotations[row.key] && hasEffect(level, row.key),
  );
  return (
    <aside className="grid content-start gap-2">
      <span className="text-[length:var(--text-xs)] font-semibold tracking-wide text-ink-muted uppercase">
        Example · this board
      </span>
      <article
        data-testid={ids.annotationsPreview}
        className="grid gap-1.5 rounded-panel border border-edge bg-panel p-2 text-[length:var(--text-sm)]"
      >
        <div className="flex items-center gap-1.5">
          <TypeIcon type={card.type} />
          <span className="font-medium text-ink">{card.title}</span>
        </div>
        <div className="flex flex-wrap gap-1.5 text-[length:var(--text-xs)] text-ink-muted">
          {shown.map((row) => (
            <span
              key={row.key}
              data-testid={ids.annotationsPreviewRollup}
              data-annotation={row.key}
            >
              {rollUp(card.counts, row.label)}
            </span>
          ))}
        </div>
      </article>
    </aside>
  );
}

/** The part of the sample's counts that belongs to one roll-up (`3/5 stories` for User stories). */
function rollUp(counts: string, label: string): string {
  const word = label.toLowerCase().replace('user ', '');
  return counts.split(' · ').find((part) => part.endsWith(word)) ?? counts;
}

import type { ComponentType } from 'react';
import type { InspectionViewProps } from '../InspectionViewProps';
import { inspectionMetricsTestIds as ids } from '../testIds';

/**
 * Quality (SHIP-1398 QK3): escaping bugs per sprint. With no bugs in the project the view model's quality is null and the
 * view says so.
 */
// TODO: a placeholder that shows only the empty state; the Quality view (cards, chart, period rows and bug list) replaces it.
export const QualityView: ComponentType<InspectionViewProps> = ({ view }) => (
  <section data-testid={ids.quality} className="grid gap-3">
    <h2 className="text-[length:var(--text-lg)] font-semibold">Quality</h2>
    {view.quality === null ? (
      <p data-testid={ids.qualityEmpty} className="text-ink-muted">
        No bugs yet. Escaping bugs show here once the project has bugs.
      </p>
    ) : null}
  </section>
);

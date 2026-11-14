import { featureTestIds } from '../feature/testIds';
import type { FeatureTimelineSectionProps } from './FeatureTimelineSectionProps';

// TODO: replaced by the Feature page's Timeline section (key dates and the Open timeline link).
export function FeatureTimelineSection({ projectKey }: FeatureTimelineSectionProps) {
  return <section data-testid={featureTestIds.featureTimelineSection} data-project={projectKey} />;
}

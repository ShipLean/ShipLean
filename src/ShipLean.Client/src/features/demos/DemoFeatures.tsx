import type { DemoProps } from './DemoProps';
import { demosTestIds as ids } from './testIds';

// TODO: the feature list with state chips and links.
export function DemoFeatures({ demo }: DemoProps) {
  return <div data-testid={ids.demoFeatures}>{demo.features.length} features</div>;
}

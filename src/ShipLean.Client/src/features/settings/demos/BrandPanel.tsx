import type { ProjectDemoBrandDto } from '../../../api/demos';
import type { ProjectDto } from '../../../api/projects';
import { demosTestIds as ids } from '../../demos/testIds';

export interface BrandPanelProps {
  project: ProjectDto;
  brand: ProjectDemoBrandDto;
  readOnly: boolean;
}

// TODO: the logo upload and preview, and the design tokens editor with its hints.
export function BrandPanel({ brand }: BrandPanelProps) {
  return (
    <section data-testid={ids.demoBrandPanel}>
      Logo: {brand.logoUrl === null ? 'ShipLean default' : 'custom'}
    </section>
  );
}

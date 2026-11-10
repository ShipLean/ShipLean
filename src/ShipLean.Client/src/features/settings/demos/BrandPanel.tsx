import type { ProjectDemoBrandDto } from '@shiplean/core/api/demos';
import type { ProjectDto } from '@shiplean/core/api/projects';
import { demosTestIds as ids } from '../../demos/testIds';
import { Panel } from '../draft/Panel';
import { LogoField } from './brand/LogoField';
import { TokensField } from './brand/TokensField';

export interface BrandPanelProps {
  project: ProjectDto;
  brand: ProjectDemoBrandDto;
  readOnly: boolean;
}

/** The logo and design tokens a demo's slides use. Each part saves at once with its own button, never the draft. */
export function BrandPanel({ project, brand, readOnly }: BrandPanelProps) {
  return (
    <Panel title="Demo brand" testId={ids.demoBrandPanel}>
      <p className="text-[length:var(--text-sm)] text-ink-muted">
        The logo and colours a demo&apos;s slides use. Nothing set means ShipLean&apos;s.
      </p>
      <LogoField projectId={project.id} brand={brand} readOnly={readOnly} />
      <TokensField key={brand.tokens} projectId={project.id} brand={brand} readOnly={readOnly} />
    </Panel>
  );
}

import type { ReactNode } from 'react';
import { FeatureAttachmentsRail } from './attachments/FeatureAttachmentsRail';
import { StoriesAndBugs } from './children/StoriesAndBugs';
import { FeatureDropZone } from './drop/FeatureDropZone';
import type { FeatureRegionProps } from './FeatureRegionProps';
import { GoalSection } from './goal/GoalSection';
import { FeatureHistory } from './history/FeatureHistory';
import { FeatureLinksSection } from './links/FeatureLinksSection';
import { FeatureLog } from './log/FeatureLog';
import { StatusBand } from './statusBand/StatusBand';
import { FeatureFieldsStrip } from './strip/FeatureFieldsStrip';
import { featureTestIds as ids } from './testIds';

// The Feature's layout (feature.html): the header, the field strip and the status band across the full width, then the
// main column (Goal, Description, Links, Stories and bugs, Agent, Log) beside the rail (Attachments, History), all inside the
// drop zone. The work item form owns the draft and Save and hands in its header, banners and Description.
export function FeatureForm({
  region,
  header,
  banners,
  description,
  agent,
}: {
  region: FeatureRegionProps;
  header: ReactNode;
  banners: ReactNode;
  description: ReactNode;
  agent: ReactNode;
}) {
  return (
    <FeatureDropZone {...region}>
      <div data-testid={ids.form}>
        {header}
        <FeatureFieldsStrip {...region} />
        {banners}
        <StatusBand {...region} />
        <div className="grid grid-cols-[minmax(0,1fr)_20rem] gap-4 p-4">
          <div
            data-testid={ids.main}
            className="grid grid-cols-[minmax(0,1fr)] content-start gap-6"
          >
            <GoalSection {...region} />
            {description}
            <FeatureLinksSection {...region} />
            <StoriesAndBugs {...region} />
            {agent}
            <FeatureLog {...region} />
          </div>
          <aside
            data-testid={ids.rail}
            aria-label="Attachments and history"
            className="grid content-start gap-4"
          >
            <FeatureAttachmentsRail {...region} />
            <FeatureHistory {...region} />
          </aside>
        </div>
      </div>
    </FeatureDropZone>
  );
}

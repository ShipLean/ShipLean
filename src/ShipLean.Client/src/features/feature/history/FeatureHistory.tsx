import { useProjectByKey } from '../../../realtime/useProjectByKey';
import { HistorySection } from '../../workItem/side/HistorySection';
import { ImportedFromSection } from '../../workItem/side/ImportedFromSection';
import type { FeatureRegionProps } from '../FeatureRegionProps';

// The story form's History, and Imported from for an item that came from Jira, under the Feature's attachments.
export function FeatureHistory({ item, projectKey }: FeatureRegionProps) {
  const project = useProjectByKey(projectKey).data;
  if (!project) return null;
  return (
    <>
      <HistorySection project={project} itemKey={item.key} />
      {item.originalSystem ? (
        <ImportedFromSection original={item.originalSystem} itemKey={item.key} />
      ) : null}
    </>
  );
}

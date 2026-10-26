import { useProjectByKey } from '../../../realtime/useProjectByKey';
import { DiscussionSection, type LogMode } from '../../workItem/discussion/DiscussionSection';
import type { FeatureRegionProps } from '../FeatureRegionProps';
import { featureTestIds as ids } from '../testIds';

// The Feature's Log: the Discussion listed newest first, agent entries marked via MCP, with Mark superseded.
const logMode: LogMode = {
  title: 'Log',
  countTestId: ids.logCount,
  emptyTestId: ids.logEmpty,
  emptyText: 'No log entries yet.',
};

export function FeatureLog({ item, projectKey }: FeatureRegionProps) {
  const project = useProjectByKey(projectKey).data;
  return (
    <div data-testid={ids.log}>
      {project ? <DiscussionSection project={project} itemKey={item.key} log={logMode} /> : null}
    </div>
  );
}

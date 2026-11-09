import type { ProjectDto } from '@shiplean/core/api/projects';
import { RetroActionDialogProvider } from '../../retroShared/actionDialog/RetroActionDialogProvider';
import { RetroQueryState } from '../RetroQueryState';
import { retrosTestIds as ids } from '../testIds';
import { useRetroItem } from '../useRetroQueries';
import { ItemBoard } from './ItemBoard';
import { ItemChildRows } from './ItemChildRows';
import { ItemFailureLogs } from './ItemFailureLogs';
import { ItemHeader } from './ItemHeader';
import { ItemRail } from './ItemRail';
import { ItemToolbar } from './ItemToolbar';
import { useRetroItemAddress } from './useRetroItemAddress';

/**
 * One item's retros: the header, the filters and one board over the item's retros, then the failure logs, the story rows
 * and rail (I2's parts). The retro filter, decide and point live in the address (retroItemAddress.ts).
 */
export function RetroItemPanel({ project, itemKey }: { project: ProjectDto; itemKey: string }) {
  const { address, setRetro, setDecide, onAdded } = useRetroItemAddress();
  const query = useRetroItem(project.id, itemKey);
  return (
    <RetroActionDialogProvider projectId={project.id}>
      <RetroQueryState query={query}>
        {(item) => (
          <div
            data-testid={ids.retroItemPanel}
            data-project={project.key}
            data-item={itemKey}
            className="grid min-w-0 grid-cols-[minmax(0,1fr)_18rem] gap-4"
          >
            <div className="grid min-w-0 content-start gap-4">
              <ItemHeader projectKey={project.key} item={item} />
              <ItemToolbar item={item} address={address} onRetro={setRetro} onDecide={setDecide} />
              <ItemBoard item={item} address={address} onAdded={onAdded} />
              <ItemFailureLogs item={item} address={address} />
              <ItemChildRows projectKey={project.key} item={item} />
            </div>
            <ItemRail projectKey={project.key} item={item} />
          </div>
        )}
      </RetroQueryState>
    </RetroActionDialogProvider>
  );
}

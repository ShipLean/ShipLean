import { useState } from 'react';
import type { ProjectDto } from '@shiplean/core/api/projects';
import { RetroActionDialogProvider } from '../../retroShared/actionDialog/RetroActionDialogProvider';
import { routePath } from '../../../routes';
import { useTopBar } from '../../../touch/TopBar';
import { RetroQueryState } from '../RetroQueryState';
import { retrosTestIds as ids } from '../testIds';
import { useRetroItem } from '../useRetroQueries';
import { ItemBoard } from './ItemBoard';
import { ItemChildRows } from './ItemChildRows';
import { ItemFailureLogs } from './ItemFailureLogs';
import { ItemHeader } from './ItemHeader';
import { ItemRail } from './ItemRail';
import { ItemToolbar } from './ItemToolbar';
import { useTouchShell } from '../../../touch/useTouchShell';
import { ItemRailSheets, type RailSheet } from './touch/ItemRailSheets';
import { useRetroItemAddress } from './useRetroItemAddress';

/**
 * One item's retros: the header, the filters and one board over the item's retros, then the failure logs, the story rows
 * and rail (I2's parts). The retro filter, decide and point live in the address (retroItemAddress.ts).
 */
export function RetroItemPanel({ project, itemKey }: { project: ProjectDto; itemKey: string }) {
  const { address, setRetro, setDecide, onAdded } = useRetroItemAddress();
  const query = useRetroItem(project.id, itemKey);
  const { touch } = useTouchShell();
  const [sheet, setSheet] = useState<RailSheet | null>(null);
  useTopBar(
    touch
      ? { back: { to: routePath('retroFeatures', { key: project.key }), label: 'Features' } }
      : {},
  );
  return (
    <RetroActionDialogProvider projectId={project.id}>
      <RetroQueryState query={query}>
        {(item) => (
          <div
            data-testid={ids.retroItemPanel}
            data-project={project.key}
            data-item={itemKey}
            className={`grid min-w-0 gap-4 ${touch ? 'grid-cols-[minmax(0,1fr)]' : 'grid-cols-[minmax(0,1fr)_18rem]'}`}
          >
            <div className="grid min-w-0 content-start gap-4 touch:grid-cols-[minmax(0,1fr)]">
              <ItemHeader
                projectKey={project.key}
                item={item}
                toDecideOn={address.decide}
                onToDecide={() => setDecide(!address.decide)}
                onActions={() => setSheet('actions')}
              />
              <ItemToolbar
                projectKey={project.key}
                item={item}
                address={address}
                onRetro={setRetro}
                onDecide={setDecide}
                onSheet={setSheet}
              />
              <ItemBoard item={item} address={address} onAdded={onAdded} />
              <ItemFailureLogs item={item} address={address} />
              <ItemChildRows projectKey={project.key} item={item} />
            </div>
            {touch ? (
              <ItemRailSheets
                projectKey={project.key}
                item={item}
                open={sheet}
                onClose={() => setSheet(null)}
              />
            ) : (
              <ItemRail projectKey={project.key} item={item} />
            )}
          </div>
        )}
      </RetroQueryState>
    </RetroActionDialogProvider>
  );
}

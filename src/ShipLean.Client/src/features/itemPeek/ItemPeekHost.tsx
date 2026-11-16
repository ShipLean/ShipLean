import { Link } from 'react-router-dom';
import type { ProjectDto } from '@shiplean/core/api/projects';
import { typeName } from '@shiplean/core/api/workItemTypes';
import { Dialog } from '../../components/Dialog';
import { TypeIcon } from '../../components/TypeIcon';
import { useTouchShell } from '../../touch/useTouchShell';
import { workItemSlots } from '../../pages/workItemSlots';
import { routePath } from '../../routes';
import { useWorkItem } from '../workItem/workItemApi';
import { FormSlot } from '../../slots/FormSlot';
import { ItemPeekContext } from './ItemPeekContext';
import { itemPeekTestIds as ids } from './testIds';
import { useItemPeek } from './useItemPeek';

// The work item modal: shown while the address has `?item=`. Its body is the item page's own content.
export function ItemPeekHost({ project }: { project: ProjectDto }) {
  const { itemKey, close } = useItemPeek();
  return itemKey === undefined ? null : (
    <ItemPeekModal project={project} itemKey={itemKey} close={close} />
  );
}

// Its own component so the viewport is read only while the modal is open.
function ItemPeekModal({
  project,
  itemKey,
  close,
}: {
  project: ProjectDto;
  itemKey: string;
  close: () => void;
}) {
  const { touch } = useTouchShell();
  // The item page's own read, shared through the query cache: the bar names the item's type next to its key at touch.
  const item = useWorkItem(project.id, itemKey).data;
  const withType = touch && item !== undefined;
  // Above 900px the link sits in the header before ×; at 900px and below the full-screen Dialog puts it at the top right.
  const openFullPage = (
    <Link
      data-testid={ids.openFullPage}
      to={routePath('workItem', { key: project.key, itemKey })}
      className="text-[length:var(--text-sm)] text-signal hover:underline touch:inline-flex touch:min-h-[var(--touch-target)] touch:items-center touch:px-2"
    >
      {touch ? '↗ Open full page' : 'Open full page'}
    </Link>
  );
  return (
    <Dialog
      title={withType ? `${itemKey} ${typeName(item.type.id)}` : itemKey}
      titleIcon={withType ? <TypeIcon type={item.type.id} /> : undefined}
      width="1200px"
      tall
      testId={ids.dialog}
      closeTestId={ids.close}
      onClose={close}
      onBackdropClick={close}
      headerExtra={touch ? undefined : openFullPage}
      primaryAction={touch ? openFullPage : undefined}
    >
      <ItemPeekContext.Provider value>
        <FormSlot {...workItemSlots(project, itemKey).form} />
      </ItemPeekContext.Provider>
    </Dialog>
  );
}

import { Link } from 'react-router-dom';
import type { ProjectDto } from '../../api/projects';
import { Dialog } from '../../components/Dialog';
import { workItemSlots } from '../../pages/workItemSlots';
import { routePath } from '../../routes';
import { FormSlot } from '../../slots/FormSlot';
import { ItemPeekContext } from './ItemPeekContext';
import { itemPeekTestIds as ids } from './testIds';
import { useItemPeek } from './useItemPeek';

// The work item modal: shown while the address has `?item=`. Its body is the item page's own content.
export function ItemPeekHost({ project }: { project: ProjectDto }) {
  const { itemKey, close } = useItemPeek();
  if (itemKey === undefined) return null;
  return (
    <Dialog
      title={itemKey}
      width="1200px"
      tall
      testId={ids.dialog}
      closeTestId={ids.close}
      onClose={close}
      onBackdropClick={close}
      headerExtra={
        <Link
          data-testid={ids.openFullPage}
          to={routePath('workItem', { key: project.key, itemKey })}
          className="text-[length:var(--text-sm)] text-signal hover:underline"
        >
          Open full page
        </Link>
      }
    >
      <ItemPeekContext.Provider value>
        <FormSlot {...workItemSlots(project, itemKey).form} />
      </ItemPeekContext.Provider>
    </Dialog>
  );
}

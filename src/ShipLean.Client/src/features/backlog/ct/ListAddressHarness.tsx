import { useLocation } from 'react-router-dom';
import type { ProjectDto } from '@shiplean/core/api/projects';
import { ListMode } from '../list/ListMode';
import { rowActionsHarnessIds } from './rowActionsHarnessIds';

// CT-only: the Stories List with the address shown, so a test sees whether a tap opened an item.
export function ListAddressHarness({ project }: { project: ProjectDto }) {
  const { pathname, search } = useLocation();
  return (
    <>
      <output data-testid={rowActionsHarnessIds.address}>{pathname + search}</output>
      <ListMode project={project} />
    </>
  );
}

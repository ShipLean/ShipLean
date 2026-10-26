import { useNavigate, useLocation } from 'react-router-dom';
import { matchRoute, routePath } from '../../../routes';
import { opensInModal } from '../../itemPeek/opensInModal';
import { useItemPeek } from '../../itemPeek/useItemPeek';

// Opens an item the way ItemLink does: in the modal over the page you are on when that fits, else on its own page.
export function useOpenItem(): (item: { key: string; projectKey: string }) => void {
  const { pathname, search } = useLocation();
  const navigate = useNavigate();
  const peek = useItemPeek();
  const here = matchRoute(pathname, search);
  const openKey = here !== undefined && 'key' in here.params ? here.params.key : undefined;
  return (item) => {
    if (opensInModal(here, openKey, item.projectKey)) peek.open(item.key);
    else void navigate(routePath('workItem', { key: item.projectKey, itemKey: item.key }));
  };
}

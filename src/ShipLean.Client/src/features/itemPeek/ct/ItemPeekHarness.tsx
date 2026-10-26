import { useLocation } from 'react-router-dom';
import { AppRoutes } from '../../../AppRoutes';
import { ItemLink } from '../ItemLink';

// The real routes plus an item link outside them (no board card links through ItemLink yet), and the address.
export function ItemPeekHarness({ itemKey }: { itemKey: string }) {
  const { pathname, search } = useLocation();
  return (
    <>
      <AppRoutes />
      <ItemLink itemKey={itemKey}>open {itemKey}</ItemLink>
      <output data-testid="harness-address">{pathname + search}</output>
    </>
  );
}

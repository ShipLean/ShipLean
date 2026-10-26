import { useLocation, useNavigate } from 'react-router-dom';
import { AppRoutes } from '../../../AppRoutes';
import { ItemLink } from '../ItemLink';
import { itemPeekTestIds } from '../testIds';

// The real routes plus an item link outside them (no board card links through ItemLink yet), and the address.
export function ItemPeekHarness({ itemKey }: { itemKey: string }) {
  const { pathname, search } = useLocation();
  const navigate = useNavigate();
  return (
    <>
      <AppRoutes />
      <ItemLink itemKey={itemKey}>open {itemKey}</ItemLink>
      {/* The CT router is in memory, so the browser's Back button does nothing; this one steps its history. */}
      <button
        type="button"
        data-testid={itemPeekTestIds.harnessBack}
        onClick={() => void navigate(-1)}
      >
        back
      </button>
      <output data-testid={itemPeekTestIds.address}>{pathname + search}</output>
    </>
  );
}

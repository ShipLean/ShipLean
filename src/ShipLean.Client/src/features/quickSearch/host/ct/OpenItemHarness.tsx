import { Route, Routes, useLocation } from 'react-router-dom';
import { useOpenItem } from '../useOpenItem';
import { openItemHarnessIds } from './harnessIds';

function Page() {
  const { pathname, search } = useLocation();
  const openItem = useOpenItem();
  return (
    <>
      <button
        type="button"
        data-testid={openItemHarnessIds.open}
        onClick={() => openItem({ key: 'SHIP-9', projectKey: 'SHIP' })}
      >
        open
      </button>
      <output data-testid={openItemHarnessIds.location}>{pathname + search}</output>
    </>
  );
}

// A button that opens SHIP-9 through useOpenItem, and the address it leaves.
export function OpenItemHarness() {
  return (
    <Routes>
      <Route path="*" element={<Page />} />
    </Routes>
  );
}

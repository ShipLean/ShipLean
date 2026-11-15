import { useLocation } from 'react-router-dom';
import { taskboardTestIds as ids } from '../testIds';

// The CT router is in memory, so the page's own URL never changes: this shows the address a test asserts on.
export function AddressProbe() {
  const { pathname, search } = useLocation();
  return <output data-testid={ids.address}>{pathname + search}</output>;
}

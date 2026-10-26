import { useLocation } from 'react-router-dom';

// The router address, for a spec that mounts a component outside the real routes (so no modal host) and checks where a click went.
export function AddressReadout() {
  const { pathname, search } = useLocation();
  return <output data-testid="address">{pathname + search}</output>;
}

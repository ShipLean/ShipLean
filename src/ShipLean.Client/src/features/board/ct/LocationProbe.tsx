import { useLocation } from 'react-router-dom';
import { boardTestIds as ids } from '../testIds';

// Shows the router's current path and query, so a spec can see where a link or an opener went (CT only).
export function LocationProbe() {
  const location = useLocation();
  return (
    <span data-testid={ids.locationProbe}>
      {location.pathname}
      {location.search}
    </span>
  );
}

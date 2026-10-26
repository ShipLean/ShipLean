import { taskboardSettingsTestIds } from '../testIds';

import { useLocation } from 'react-router-dom';

// Component-test stand-in: shows the router's query string so a test can read what a click did to the URL.
export function LocationProbe() {
  return (
    <output data-testid={taskboardSettingsTestIds.locationProbe}>{useLocation().search}</output>
  );
}

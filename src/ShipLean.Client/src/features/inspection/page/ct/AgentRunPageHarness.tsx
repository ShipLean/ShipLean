import { Route, Routes } from 'react-router-dom';
import { AgentRunPage } from '../../../../pages/AgentRunPage';
import { routePattern } from '../../../../routes';

// The run route as the app mounts it, so the page reads its keys from the address and the width decides what shows.
export function AgentRunPageHarness() {
  return (
    <Routes>
      <Route path={routePattern('agentRun')} element={<AgentRunPage />} />
    </Routes>
  );
}

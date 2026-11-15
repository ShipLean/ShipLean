import { ToastHost } from '../../../../../components/ToastHost';
import { ConnectedAgentsPanel } from '../../../../agents/connected/ConnectedAgentsPanel';
import { MyProjectsPanel } from '../MyProjectsPanel';
import { SessionsPanel } from '../SessionsPanel';

/** Your projects, Sessions and Connected agents in a column with the toast host, for the phone spec. */
export function AccountPanelsHarness() {
  return (
    <div className="grid gap-3 p-3">
      <MyProjectsPanel />
      <SessionsPanel />
      <ConnectedAgentsPanel />
      <ToastHost />
    </div>
  );
}

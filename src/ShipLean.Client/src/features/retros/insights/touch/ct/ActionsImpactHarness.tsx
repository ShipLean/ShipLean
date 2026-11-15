import { useLocation } from 'react-router-dom';
import type { RetroInsightsDto } from '@shiplean/core/api/retros';
import { RetroActionDialogProvider } from '../../../../retroShared/actionDialog/RetroActionDialogProvider';
import { ActionItemsTable } from '../../ActionItemsTable';
import { ChangesImpact } from '../../ChangesImpact';
import { insightsProject } from '../../ct/insightsProject';

// The Action items or Changes & impact view beside the router's current path and query, so a spec can see what a tap wrote
// to the address (CT only).
export function ActionsImpactHarness({
  view,
  insights,
}: {
  view: 'actions' | 'impact';
  insights: RetroInsightsDto;
}) {
  const location = useLocation();
  return (
    <RetroActionDialogProvider projectId={insightsProject.id}>
      {view === 'actions' ? (
        <ActionItemsTable project={insightsProject} insights={insights} />
      ) : (
        <ChangesImpact project={insightsProject} insights={insights} />
      )}
      <span data-testid="location-probe">
        {location.pathname}
        {location.search}
      </span>
    </RetroActionDialogProvider>
  );
}

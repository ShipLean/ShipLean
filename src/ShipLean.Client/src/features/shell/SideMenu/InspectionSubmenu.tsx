import { routePath, type MatchedRoute } from '../../../routes';
import { inspectionMetricsTestIds as ids } from '../../inspectionMetrics/testIds';
import { NavItem } from './NavItem';

// Under Inspection: Overview, Lead time, Quality and Cost. A sprint stays under Lead time and the JSON under Overview, as
// the design's side menu lights them. Cost is always listed: the client has no project-level agent-runs flag, so the Cost
// view says when agent runs are off.
export function InspectionSubmenu({
  projectKey,
  route,
}: {
  projectKey: string;
  route: MatchedRoute | undefined;
}) {
  const view = route?.id === 'inspectionMetrics' ? (route.query.view ?? 'overview') : undefined;
  const onLeadTime = view === 'leadtime' || view === 'period';
  const onQuality = view === 'quality';
  const onCost = view === 'cost';
  return (
    <>
      <NavItem
        sub
        testId={ids.menuOverview}
        to={routePath('inspectionMetrics', { key: projectKey })}
        active={view !== undefined && !onLeadTime && !onQuality && !onCost}
      >
        Overview
      </NavItem>
      <NavItem
        sub
        testId={ids.menuLeadTime}
        to={routePath('inspectionMetrics', { key: projectKey }, { view: 'leadtime' })}
        active={onLeadTime}
      >
        Lead time
      </NavItem>
      <NavItem
        sub
        testId={ids.menuQuality}
        to={routePath('inspectionMetrics', { key: projectKey }, { view: 'quality' })}
        active={onQuality}
      >
        Quality
      </NavItem>
      <NavItem
        sub
        testId={ids.menuCost}
        to={routePath('inspectionMetrics', { key: projectKey }, { view: 'cost' })}
        active={onCost}
      >
        Cost
      </NavItem>
    </>
  );
}

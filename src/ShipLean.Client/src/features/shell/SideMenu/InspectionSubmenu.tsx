import { routePath, type MatchedRoute } from '../../../routes';
import { inspectionMetricsTestIds as ids } from '../../inspectionMetrics/testIds';
import { NavItem } from './NavItem';

// Under Inspection: Overview and Lead time (Quality and Cost join in their waves). A sprint stays under Lead time and the
// JSON under Overview, as the design's side menu lights them.
export function InspectionSubmenu({
  projectKey,
  route,
}: {
  projectKey: string;
  route: MatchedRoute | undefined;
}) {
  const view = route?.id === 'inspectionMetrics' ? (route.query.view ?? 'overview') : undefined;
  const onLeadTime = view === 'leadtime' || view === 'period';
  return (
    <>
      <NavItem
        sub
        testId={ids.menuOverview}
        to={routePath('inspectionMetrics', { key: projectKey })}
        active={view !== undefined && !onLeadTime}
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
    </>
  );
}

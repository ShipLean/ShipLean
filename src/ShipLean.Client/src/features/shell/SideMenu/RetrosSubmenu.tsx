import { Chip } from '../../../components/Chip';
import { routePath, type MatchedRoute } from '../../../routes';
import { useRetroInsights } from '../../retros/useRetroQueries';
import { shellTestIds as ids } from '../testIds';
import { NavItem } from './NavItem';

// Under Retrospectives: Search, Features, and Insights. While you are in Retrospectives (`counted`), Insights also says how
// many themes are recurring; the touch drawer lists the three from every section without fetching the count.
export function RetrosSubmenu({
  projectId,
  projectKey,
  route,
  counted = true,
}: {
  projectId: string;
  projectKey: string;
  route: MatchedRoute | undefined;
  counted?: boolean;
}) {
  return (
    <>
      <NavItem
        sub
        testId={ids.navRetrosSearch}
        to={routePath('retros', { key: projectKey })}
        active={route?.id === 'retros'}
      >
        Search
      </NavItem>
      <NavItem
        sub
        testId={ids.navRetrosFeatures}
        to={routePath('retroFeatures', { key: projectKey })}
        active={route?.id === 'retroFeatures' || route?.id === 'retroItem'}
      >
        Features
      </NavItem>
      <NavItem
        sub
        testId={ids.navRetrosInsights}
        to={routePath('retroInsights', { key: projectKey })}
        active={route?.id === 'retroInsights'}
      >
        Insights
        {counted ? <RecurringCount projectId={projectId} /> : null}
      </NavItem>
    </>
  );
}

function RecurringCount({ projectId }: { projectId: string }) {
  const insights = useRetroInsights(projectId, '6');
  const recurring = insights.data?.themes.filter((theme) => theme.status === 'recurring').length;
  return recurring ? (
    <span className="ml-auto" data-testid={ids.navRetrosInsightsCount}>
      <Chip tone="warn">{recurring} recurring</Chip>
    </span>
  ) : null;
}

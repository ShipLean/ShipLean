import { Chip } from '../../../components/Chip';
import { routePath, type MatchedRoute } from '../../../routes';
import { useRetroInsights } from '../../retros/useRetroQueries';
import { shellTestIds as ids } from '../testIds';
import { NavItem } from './NavItem';

// Under Retrospectives while you are in it: Search, and Insights with how many themes are recurring.
export function RetrosSubmenu({
  projectId,
  projectKey,
  route,
}: {
  projectId: string;
  projectKey: string;
  route: MatchedRoute | undefined;
}) {
  const insights = useRetroInsights(projectId, '6');
  const recurring = insights.data?.themes.filter((theme) => theme.status === 'recurring').length;
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
        testId={ids.navRetrosInsights}
        to={routePath('retroInsights', { key: projectKey })}
        active={route?.id === 'retroInsights'}
      >
        Insights
        {recurring ? (
          <span className="ml-auto" data-testid={ids.navRetrosInsightsCount}>
            <Chip tone="accent">{recurring}</Chip>
          </span>
        ) : null}
      </NavItem>
    </>
  );
}

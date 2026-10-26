import { useSearchParams } from 'react-router-dom';
import { ConsentCard } from '../features/agents/authorize/ConsentCard';
import { agentsTestIds } from '../features/agents/testIds';
import { InstallScreen } from './InstallScreen';

export function AuthorizePage() {
  const [params] = useSearchParams();
  const requestId = params.get('request');
  return (
    <InstallScreen routeId="authorize" testId={agentsTestIds.authorizePage}>
      <ConsentCard requestId={requestId === null || requestId === '' ? null : requestId} />
    </InstallScreen>
  );
}

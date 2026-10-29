import { useQuery } from '@tanstack/react-query';
import { api } from '../../../api/client';
import { settingsTestIds } from '../testIds';

interface HealthBody {
  publicOrigin?: string;
}

/** Shown when agent links fell back to a cluster service name and this instance still has no base URL. */
export function PublicOriginNotice({ baseUrl }: { baseUrl: string }) {
  const health = useQuery({
    queryKey: ['health', 'public-origin'],
    queryFn: () => api.get<HealthBody>('/api/health'),
    enabled: baseUrl.trim() === '',
  });
  if (health.data?.publicOrigin !== 'unknown: set base_url') return null;
  return (
    <span
      data-testid={settingsTestIds.instancePublicOrigin}
      className="text-[length:var(--text-sm)] text-caution"
    >
      Agents are getting links on a cluster service name. Set the base URL, or trust the gateway and
      have it send X-Forwarded-Host.
    </span>
  );
}

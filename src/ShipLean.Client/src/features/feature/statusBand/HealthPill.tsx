import type { FeatureHealth } from '@shiplean/core/api/screens';
import { Chip } from '../../../components/Chip';

// Only a Feature that is At risk or Blocked gets a pill; On track and unset draw nothing (the same rule as the phone).
export function HealthPill({ health, testId }: { health: FeatureHealth | null; testId: string }) {
  if (health !== 'atRisk' && health !== 'blocked') return null;
  return (
    <span data-testid={testId} data-health={health} className="inline-flex">
      <Chip tone={health === 'blocked' ? 'danger' : 'warn'}>
        {health === 'blocked' ? 'Blocked' : 'At risk'}
      </Chip>
    </span>
  );
}

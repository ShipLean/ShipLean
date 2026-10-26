import type { KeyCheckDto } from '../../../api/projects';
import { importTestIds as ids } from '../testIds';

function reasonText(check: KeyCheckDto): string {
  if (check.reason === 'invalid')
    return `${check.key} is not a valid key: 2 to 4 uppercase letters, A to Z.`;
  return `${check.key} is already used by a project.`;
}

export function KeyCheckResult({ check }: { check: KeyCheckDto | undefined }) {
  if (check === undefined)
    return (
      <p data-testid={ids.createKeyResult} className="text-ink-muted">
        Checking the key…
      </p>
    );
  if (check.available) {
    return (
      <p data-testid={ids.createKeyResult} data-available="true" className="text-good">
        {check.key} is available.
      </p>
    );
  }
  return (
    <p data-testid={ids.createKeyResult} data-available="false" role="alert" className="text-alert">
      {reasonText(check)}
      {check.alternatives.length > 0 ? ` Try ${check.alternatives.join(', ')}.` : ''}
    </p>
  );
}

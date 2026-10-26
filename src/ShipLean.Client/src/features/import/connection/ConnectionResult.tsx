import type { JiraTestResultDto } from '../../../api/import';
import { importTestIds as ids } from '../testIds';

// What Test connection found: who you are connected as, or what to check.
export function ConnectionResult({ result }: { result: JiraTestResultDto }) {
  if (result.ok) {
    return (
      <p data-testid={ids.testResult} data-ok="true" role="status" className="text-good">
        <b>Connected as {result.who?.displayName ?? 'Jira'}</b>
        {result.who?.email ? ` (${result.who.email})` : ''}
      </p>
    );
  }
  return (
    <p data-testid={ids.testResult} data-ok="false" role="alert" className="text-alert">
      <b>The connection did not work. What to check:</b> {result.message}
    </p>
  );
}

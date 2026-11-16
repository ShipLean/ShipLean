import { settingsTestIds as ids } from '../../testIds';
import { errorText } from '../model';
import { useConnectionHealth, useTestConnection } from './agentsApi';
import type { AgentConnectionDto, AgentConnectionHealth, AgentConnectionTest } from './model';

/** The last delivery and the failures of the last 24 hours, then Test connection with what it answered. Agent text is plain text. */
export function ConnectionHealth({ connection }: { connection: AgentConnectionDto }) {
  const health = useConnectionHealth(connection.id);
  const test = useTestConnection();
  const archived = connection.archivedAt !== null;
  return (
    <div
      data-testid={ids.agentsConnectionHealth}
      className="grid gap-1 text-[length:var(--text-sm)]"
    >
      {health.data ? <HealthLines health={health.data} /> : null}
      {health.error ? (
        <p role="alert" className="m-0 text-alert">
          {errorText(health.error)}
        </p>
      ) : null}
      {archived ? null : (
        <div className="flex flex-wrap items-center gap-2">
          <button
            type="button"
            data-testid={ids.agentsConnectionTest}
            className="btn"
            disabled={test.isPending}
            onClick={() => test.mutate(connection.id)}
          >
            {test.isPending ? 'Testing…' : 'Test connection'}
          </button>
          {test.data ? (
            <span
              role={test.data.reached ? 'status' : 'alert'}
              data-testid={ids.agentsConnectionTestResult}
              className={test.data.reached ? 'text-good' : 'text-alert'}
            >
              {testAnswerText(test.data)}
            </span>
          ) : null}
          {test.error ? (
            <span role="alert" data-testid={ids.agentsConnectionTestResult} className="text-alert">
              {errorText(test.error)}
            </span>
          ) : null}
        </div>
      )}
    </div>
  );
}

function HealthLines({ health }: { health: AgentConnectionHealth }) {
  return (
    <>
      <p className="m-0 text-ink-muted">
        {health.lastDeliveredAt === null
          ? 'No delivery yet'
          : `Last delivered ${health.lastDeliveredLabel ?? ''}`.trim()}
      </p>
      {health.failedLast24h > 0 ? (
        <p className="m-0 text-alert">
          {`${health.failedLast24h} failed in the last 24 h`}
          {health.lastFailedLabel === null ? '' : `, the last ${health.lastFailedLabel}`}
          {health.lastError === null ? '' : `: ${health.lastError}`}
        </p>
      ) : null}
    </>
  );
}

// The names .NET gives these statuses, so the line reads the same as the server's own words ("Answered 502 BadGateway").
const names: Record<number, string> = {
  200: 'OK',
  201: 'Created',
  202: 'Accepted',
  204: 'NoContent',
  301: 'MovedPermanently',
  302: 'Found',
  307: 'TemporaryRedirect',
  308: 'PermanentRedirect',
  400: 'BadRequest',
  401: 'Unauthorized',
  403: 'Forbidden',
  404: 'NotFound',
  405: 'MethodNotAllowed',
  408: 'RequestTimeout',
  409: 'Conflict',
  410: 'Gone',
  415: 'UnsupportedMediaType',
  422: 'UnprocessableEntity',
  429: 'TooManyRequests',
  500: 'InternalServerError',
  501: 'NotImplemented',
  502: 'BadGateway',
  503: 'ServiceUnavailable',
  504: 'GatewayTimeout',
};

/** What Test connection says: the status any HTTP answer carried, or the error when nothing answered. */
export function testAnswerText(result: AgentConnectionTest): string {
  if (!result.reached) return result.error ?? 'No answer';
  if (result.httpStatus === null) return 'Answered';
  const name = names[result.httpStatus];
  return `Answered ${result.httpStatus}${name === undefined ? '' : ` ${name}`}`;
}

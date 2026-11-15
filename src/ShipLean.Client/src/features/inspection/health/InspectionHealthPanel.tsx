import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { ApiRequestError } from '@shiplean/core/api/client';
import { fetchInspectionHealth, type InspectionHealthDto } from '@shiplean/core/api/inspection';
import { AccessState } from '../../settings/instance/AccessState';
import { Panel } from '../../settings/draft/Panel';
import { inspectionTestIds as ids } from '../testIds';

export const inspectionHealthKey = ['instance', 'inspection', 'health'] as const;

const isNotFound = (error: unknown) => error instanceof ApiRequestError && error.status === 404;

/** The instance admin's Inspection tab: where the store is, whether it answers and which mapped fields it has. */
export function InspectionHealthPanel() {
  const health = useQuery({
    queryKey: inspectionHealthKey,
    queryFn: fetchInspectionHealth,
    // The route answers not_found while the instance has Inspection off, which is an answer and not a failure to retry.
    retry: false,
  });
  if (isNotFound(health.error)) return <Off />;
  return (
    <AccessState pending={health.isPending} error={health.data ? null : health.error}>
      {health.data ? (
        health.data.enabled ? (
          <Health
            data={health.data}
            checking={health.isFetching}
            onCheck={() => void health.refetch()}
          />
        ) : (
          <Off />
        )
      ) : null}
    </AccessState>
  );
}

function Off() {
  return (
    <Panel title="Inspection" testId={ids.inspectionHealth}>
      <p data-testid={ids.inspectionHealthOff} className="text-ink-muted">
        Inspection is off on this instance. Set SHIPLEAN_INSPECTION=on and a store to turn it on.
      </p>
    </Panel>
  );
}

const MODE_LABEL: Record<string, string> = {
  clickhouse: 'ClickHouse',
  tempoLoki: 'Tempo and Loki',
};

function Health({
  data,
  checking,
  onCheck,
}: {
  data: InspectionHealthDto;
  checking: boolean;
  onCheck: () => void;
}) {
  // The fields arrive with the health, but they show only once an admin asks, so the answer is the one they just got.
  const [asked, setAsked] = useState(false);
  return (
    <Panel title="Inspection" testId={ids.inspectionHealth}>
      <dl className="m-0 grid grid-cols-[max-content_1fr] gap-x-4 gap-y-1 text-[length:var(--text-sm)]">
        <dt className="text-ink-muted">Store</dt>
        <dd data-testid={ids.inspectionHealthMode} className="m-0 text-ink">
          {data.mode === null ? 'Not set' : (MODE_LABEL[data.mode] ?? data.mode)}
        </dd>
        <dt className="text-ink-muted">Reachable</dt>
        <dd
          data-testid={ids.inspectionHealthReachable}
          data-reachable={data.reachable}
          className={`m-0 ${data.reachable ? 'text-ink' : 'text-alert'}`}
        >
          {data.reachable ? 'Yes' : 'No'}
        </dd>
        {data.latencyMs !== null && (
          <>
            <dt className="text-ink-muted">Latency</dt>
            <dd data-testid={ids.inspectionHealthLatency} className="m-0 text-ink">
              {data.latencyMs} ms
            </dd>
          </>
        )}
        {data.viewLagSeconds !== null && (
          <>
            <dt className="text-ink-muted">View lag</dt>
            <dd data-testid={ids.inspectionHealthLag} className="m-0 text-ink">
              {data.viewLagSeconds} s
            </dd>
          </>
        )}
      </dl>
      {data.problem !== null && (
        <p role="alert" data-testid={ids.inspectionHealthProblem} className="m-0 text-alert">
          {data.problem}
        </p>
      )}
      <div>
        <button
          type="button"
          data-testid={ids.inspectionHealthCheckFields}
          disabled={checking}
          onClick={() => {
            setAsked(true);
            onCheck();
          }}
          className="h-[30px] rounded-control border border-edge bg-panel px-3 text-[length:var(--text-sm)] text-ink disabled:opacity-60"
        >
          Check fields
        </button>
      </div>
      {asked && !checking && (
        <ul className="m-0 grid list-none gap-1 p-0 text-[length:var(--text-sm)]">
          {data.fields.map((field) => (
            <li
              key={field.name}
              data-testid={ids.inspectionHealthField}
              data-name={field.name}
              data-found={field.found}
              className="flex gap-2"
            >
              <span className="text-ink">{field.name}</span>
              <code className="text-ink-muted">{field.attribute}</code>
              <span className={field.found ? 'text-ink' : 'text-alert'}>
                {field.found ? 'found' : 'not found'}
              </span>
            </li>
          ))}
        </ul>
      )}
    </Panel>
  );
}

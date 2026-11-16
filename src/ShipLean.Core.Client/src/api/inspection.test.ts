import { afterEach, expect, test, vi } from 'vitest';
import section from '../../../ShipLean.TestSupport/Inspection/Shapes/agent-runs-section.json';
import setup from '../../../ShipLean.TestSupport/Inspection/Shapes/agent-setup.json';
import health from '../../../ShipLean.TestSupport/Inspection/Shapes/inspection-health.json';
import settings from '../../../ShipLean.TestSupport/Inspection/Shapes/inspection-project-settings.json';
import usage from '../../../ShipLean.TestSupport/Inspection/Shapes/inspection-usage.json';
import runPage from '../../../ShipLean.TestSupport/Inspection/Shapes/run-page.json';
import rowDetail from '../../../ShipLean.TestSupport/Inspection/Shapes/run-row-detail.json';
import {
  fetchAgentRuns,
  fetchAgentSetup,
  fetchRunPage,
  fetchRunRow,
  postInspectionUsage,
  putInspectionSettings,
  INSPECTION_USAGE_ACTIONS,
  type AgentRunsSectionDto,
  type AgentSetupDto,
  type InspectionHealthDto,
  type InspectionProjectSettingsDto,
  type InspectionUsageDto,
  type RunPageDto,
  type RunRowDetailDto,
} from './inspection';

// Typecheck fails if a pinned shape lacks a field, has one of the wrong type, or has one the type doesn't declare.
type DeepWiden<T> = T extends string
  ? string
  : T extends number
    ? number
    : T extends boolean
      ? boolean
      : T extends readonly (infer U)[]
        ? DeepWiden<U>[]
        : T extends object
          ? { [K in keyof T]: DeepWiden<T[K]> }
          : T;

type NoExtraKeys<U, T> = U extends readonly (infer UE)[]
  ? T extends readonly (infer TE)[]
    ? NoExtraKeys<UE, TE>[]
    : U
  : U extends object
    ? T extends object
      ? { [K in keyof U]: K extends keyof T ? NoExtraKeys<U[K], T[K]> : never }
      : U
    : U;

function typed<T>() {
  return <U extends DeepWiden<T>>(value: U & NoExtraKeys<U, DeepWiden<T>>): DeepWiden<T> => value;
}

test('inspectionShapes_ShouldTypeEveryPinnedJson', () => {
  expect(typed<AgentRunsSectionDto>()(section).rows).toHaveLength(2);
  expect(typed<RunPageDto>()(runPage).turns.length).toBeGreaterThan(0);
  expect(typed<RunRowDetailDto>()(rowDetail).kind).toBe('tool');
  expect(typed<InspectionProjectSettingsDto>()(settings).storeMode).toBe('clickhouse');
  expect(typed<AgentSetupDto>()(setup).files).toHaveLength(1);
  expect(typed<InspectionHealthDto>()(health).fields).toHaveLength(3);
  expect(INSPECTION_USAGE_ACTIONS).toContain(typed<InspectionUsageDto>()(usage).action);
});

const calls: { method: string; url: string }[] = [];

function stubFetch() {
  calls.length = 0;
  vi.stubGlobal(
    'fetch',
    vi.fn((input: string | URL | Request, init?: RequestInit) => {
      calls.push({ method: init?.method ?? 'GET', url: String(input) });
      return Promise.resolve(new Response('{}', { status: 200 }));
    }),
  );
}

afterEach(() => vi.unstubAllGlobals());

test('fetchers_ShouldCallEachK5Route', async () => {
  stubFetch();
  await fetchAgentRuns('SHIP', 'SHIP-1');
  await fetchRunPage('SHIP', 'SHIP-1', 'sess 1', { errorsOnly: true, turn: 2 });
  await fetchRunRow('SHIP', 'SHIP-1', 'sess 1', 'r1');
  await fetchAgentSetup('SHIP', 'cursor', 'SHIP-1');
  await putInspectionSettings('SHIP', {
    enabled: true,
    showContent: false,
    tenantOverride: null,
    selectorOverride: null,
  });
  await postInspectionUsage('p-1', { action: 'runOpened', itemKey: 'SHIP-1' });
  expect(calls.map((c) => `${c.method} ${c.url}`)).toEqual([
    'GET /api/inspection/projects/SHIP/items/SHIP-1/runs',
    'GET /api/inspection/projects/SHIP/items/SHIP-1/runs/sess%201?errorsOnly=true&turn=2',
    'GET /api/inspection/projects/SHIP/items/SHIP-1/runs/sess%201/rows/r1',
    'GET /api/inspection/projects/SHIP/agent-setup?agent=cursor&item=SHIP-1',
    'PUT /api/inspection/projects/SHIP/settings',
    'POST /api/projects/p-1/usage/inspection',
  ]);
});

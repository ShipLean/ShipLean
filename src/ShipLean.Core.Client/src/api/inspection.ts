// Agent run inspection: the wire shapes and every route of /api/inspection/* (K5 of SHIP-1463), plus the usage POST.
// Pinned by ShipLean.TestSupport/Inspection/Shapes/*.json and round-tripped by inspection.test.ts. The server makes
// every label and every percentage; times are milliseconds offsets from the run start, never Dates.
import { api } from './client';

export const INSPECTION_STORE_MODES = ['clickhouse', 'tempo-loki'] as const;
export type InspectionStoreMode = (typeof INSPECTION_STORE_MODES)[number];

export const INSPECTION_AGENTS = ['claude-code', 'codex-cli', 'cursor'] as const;
export type InspectionAgent = (typeof INSPECTION_AGENTS)[number];

export const RUN_LINK_SOURCES = ['tagged', 'fromToolCall', 'shipleanFile'] as const;
export type RunLinkSource = (typeof RUN_LINK_SOURCES)[number];

export const RUN_ROW_KINDS = ['modelCall', 'tool', 'shipleanTool', 'subagent', 'event'] as const;
export type RunRowKind = (typeof RUN_ROW_KINDS)[number];

/** One mark on a run's mini timeline in the Agent runs section; `kind` is a row kind, `wait` or `error`. */
export interface RunTimelineMarkDto {
  kind: string;
  startPct: number;
  endPct: number;
}

export interface AgentRunRowDto {
  runId: string;
  agent: string;
  agentLabel: string;
  startedBy: string | null;
  startedLabel: string;
  whatItDid: string | null;
  linkSource: RunLinkSource;
  turns: number;
  spanOrEventCount: number;
  logOnly: boolean;
  model: string | null;
  tookMs: number;
  tokensIn: number | null;
  tokensOut: number | null;
  errors: number;
  timeline: RunTimelineMarkDto[];
}

export interface AgentRunsTotalsDto {
  runs: number;
  tookMs: number;
  tokensIn: number | null;
  tokensOut: number | null;
  errors: number;
  waitMs: number;
}

export interface AgentRunsSectionDto {
  storeReachable: boolean;
  problem: string | null;
  store: { mode: InspectionStoreMode; retentionDays: number };
  contentShown: boolean;
  totals: AgentRunsTotalsDto;
  rows: AgentRunRowDto[];
  setupUrl: string;
}

export interface RunHeaderDto {
  agent: string;
  agentLabel: string;
  startedLabel: string;
  startedBy: string | null;
  linkSource: RunLinkSource;
  olderRunId: string | null;
  newerRunId: string | null;
  tookMs: number;
  turns: number;
  spansOrEvents: number;
  tokensIn: number | null;
  tokensOut: number | null;
  errors: number;
  waitMs: number;
}

export interface RunWaitDto {
  startPct: number;
  endPct: number;
}

export interface RunOverviewTurnDto {
  turn: number;
  startPct: number;
  endPct: number;
  hasError: boolean;
  waits: RunWaitDto[];
}

export interface RunAxisDto {
  startMs: number;
  endMs: number;
}

export interface RunRowDto {
  id: string;
  parentId: string | null;
  depth: number;
  kind: RunRowKind;
  name: string;
  startMs: number;
  endMs: number | null;
  durationMs: number | null;
  isError: boolean;
  isRetry: boolean;
  waitStartMs: number | null;
  waitEndMs: number | null;
  collapsedChildCount: number;
  hasLogs: boolean;
}

export interface RunTurnDto {
  number: number;
  label: string;
  prompt: string | null;
  promptHidden: boolean;
  rows: RunRowDto[];
}

export interface RunPageDto {
  expired: boolean;
  header: RunHeaderDto;
  logOnly: boolean;
  banner: string | null;
  truncated: boolean;
  grafanaUrl: string | null;
  contentShown: boolean;
  overview: RunOverviewTurnDto[];
  axis: RunAxisDto;
  turns: RunTurnDto[];
}

export interface RunAttributeDto {
  key: string;
  value: string;
}

export interface RunLogDto {
  timeLabel: string;
  severity: string;
  body: string;
  attributes: RunAttributeDto[];
}

export interface RunRowDetailDto {
  kind: RunRowKind;
  name: string;
  startLabel: string;
  durationMs: number | null;
  offsetMs: number;
  waitMs: number | null;
  contentHidden: boolean;
  input: string | null;
  output: string | null;
  outputTruncated: boolean;
  attributes: RunAttributeDto[];
  logs: RunLogDto[];
  filePath: string | null;
  commitSha: string | null;
  grafanaUrl: string | null;
}

export interface InspectionProjectSettingsDto {
  enabled: boolean;
  showContent: boolean;
  tenantOverride: string | null;
  selectorOverride: string | null;
  storeMode: InspectionStoreMode;
}

/** PUT body: the three things an admin sets; the store mode is the instance's. */
export type InspectionProjectSettingsPut = Omit<InspectionProjectSettingsDto, 'storeMode'>;

export interface AgentSetupFileDto {
  name: string;
  language: string;
  content: string;
}

export interface AgentSetupDto {
  agent: string;
  agentLabel: string;
  files: AgentSetupFileDto[];
  notes: string[];
  docsUrl: string;
}

export interface InspectionHealthFieldDto {
  name: string;
  attribute: string;
  found: boolean;
}

export interface InspectionHealthDto {
  enabled: boolean;
  mode: InspectionStoreMode | null;
  reachable: boolean;
  latencyMs: number | null;
  viewLagSeconds: number | null;
  problem: string | null;
  fields: InspectionHealthFieldDto[];
}

export const INSPECTION_USAGE_ACTIONS = [
  'sectionSeen',
  'runOpened',
  'rowSelected',
  'zoomed',
  'errorsOnly',
] as const;
export type InspectionUsageAction = (typeof INSPECTION_USAGE_ACTIONS)[number];

export interface InspectionUsageDto {
  action: InspectionUsageAction;
  itemKey: string;
}

const projectPath = (project: string) => `/api/inspection/projects/${encodeURIComponent(project)}`;
const runPath = (project: string, itemKey: string, runId: string) =>
  `${projectPath(project)}/items/${encodeURIComponent(itemKey)}/runs/${encodeURIComponent(runId)}`;

/** GET …/items/{key}/runs: the Agent runs section of one item. `project` is the project key. */
export function fetchAgentRuns(project: string, itemKey: string): Promise<AgentRunsSectionDto> {
  return api.get<AgentRunsSectionDto>(
    `${projectPath(project)}/items/${encodeURIComponent(itemKey)}/runs`,
  );
}

/** GET …/runs/{runId}: the Run page, fetched once. `turn` limits it to one turn; `errorsOnly` to the failing rows. */
export function fetchRunPage(
  project: string,
  itemKey: string,
  runId: string,
  options: { errorsOnly?: boolean; turn?: number } = {},
): Promise<RunPageDto> {
  const search = new URLSearchParams();
  if (options.errorsOnly === true) search.set('errorsOnly', 'true');
  if (options.turn !== undefined) search.set('turn', String(options.turn));
  const text = search.toString();
  return api.get<RunPageDto>(`${runPath(project, itemKey, runId)}${text === '' ? '' : `?${text}`}`);
}

/** GET …/runs/{runId}/rows/{rowId}: one row's detail, fetched when it is selected. */
export function fetchRunRow(
  project: string,
  itemKey: string,
  runId: string,
  rowId: string,
): Promise<RunRowDetailDto> {
  return api.get<RunRowDetailDto>(
    `${runPath(project, itemKey, runId)}/rows/${encodeURIComponent(rowId)}`,
  );
}

/** GET api/inspection/projects/{project}/settings. */
export function fetchInspectionSettings(project: string): Promise<InspectionProjectSettingsDto> {
  return api.get<InspectionProjectSettingsDto>(`${projectPath(project)}/settings`);
}

/** PUT api/inspection/projects/{project}/settings (project admin only). */
export function putInspectionSettings(
  project: string,
  body: InspectionProjectSettingsPut,
): Promise<InspectionProjectSettingsDto> {
  return api.put<InspectionProjectSettingsDto>(`${projectPath(project)}/settings`, body);
}

/** GET …/agent-setup?agent=&item=: the files the Agent setup tab shows for one agent and, optionally, one item. */
export function fetchAgentSetup(
  project: string,
  agent: string,
  itemKey?: string,
): Promise<AgentSetupDto> {
  const search = new URLSearchParams({ agent });
  if (itemKey !== undefined && itemKey !== '') search.set('item', itemKey);
  return api.get<AgentSetupDto>(`${projectPath(project)}/agent-setup?${search.toString()}`);
}

/** GET api/inspection/health (instance admin). */
export function fetchInspectionHealth(): Promise<InspectionHealthDto> {
  return api.get<InspectionHealthDto>('/api/inspection/health');
}

/** POST api/projects/{projectId}/usage/inspection: one viewing event (D22). The server answers 204. */
export function postInspectionUsage(projectId: string, body: InspectionUsageDto): Promise<void> {
  return api.post<void>(`/api/projects/${encodeURIComponent(projectId)}/usage/inspection`, body);
}

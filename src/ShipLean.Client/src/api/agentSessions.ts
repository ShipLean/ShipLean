// The Agent panel on a work item page. C#: ShipLean.Contracts.Agents (AgentPanelDto and its parts). Nothing is omitted: absent
// values are null; timestamps are ISO strings and labels are formatted by the server. Text from an agent is untrusted: render
// it as text only.

export const AGENT_START_BLOCKS = ['noConnection', 'noRepository', 'liveSession'] as const;
export type AgentStartBlock = (typeof AGENT_START_BLOCKS)[number];

export interface AgentStartAvailabilityDto {
  canStart: boolean;
  /** Null when Start agent works. */
  reason: AgentStartBlock | null;
  /** Empty when Start agent works; otherwise a sentence for a person. */
  message: string;
}

export const AGENT_SESSION_STATES = [
  'submitted',
  'working',
  'inputRequired',
  'completed',
  'failed',
  'canceled',
  'stale',
] as const;
export type AgentSessionState = (typeof AGENT_SESSION_STATES)[number];

export const AGENT_ACTIVITY_TYPES = [
  'thought',
  'action',
  'elicitation',
  'response',
  'error',
  'prompt',
] as const;
export type AgentActivityType = (typeof AGENT_ACTIVITY_TYPES)[number];

export const AGENT_PLAN_STATUSES = ['pending', 'inProgress', 'completed', 'canceled'] as const;
export type AgentPlanStatus = (typeof AGENT_PLAN_STATUSES)[number];

export interface AgentUrlDto {
  label: string;
  url: string;
}

export interface AgentPlanStepDto {
  content: string;
  status: AgentPlanStatus;
}

export interface AgentActivityDto {
  id: string;
  type: AgentActivityType;
  body: string | null;
  action: string | null;
  parameter: string | null;
  result: string | null;
  /** A transient status line: the server sends at most one, and only when it is the newest activity. */
  ephemeral: boolean;
  /** Set on a prompt that carries one ("stop"). */
  signal: string | null;
  createdAt: string;
  createdLabel: string;
}

export interface AgentSessionDto {
  id: string;
  connectionName: string;
  state: AgentSessionState;
  /** Submitted, working or waiting for input. */
  live: boolean;
  /** Stop was pressed and the agent has not answered yet. */
  stopRequested: boolean;
  startedAt: string;
  startedLabel: string;
  lastActivityAt: string;
  lastActivityLabel: string;
  /** Null while the session has not ended. */
  endedLabel: string | null;
  urls: AgentUrlDto[];
  plan: AgentPlanStepDto[];
  /** Oldest first. */
  activities: AgentActivityDto[];
}

export interface AgentPanelDto {
  /** False when the item has no session and the project has no agent connection: show nothing. */
  visible: boolean;
  availability: AgentStartAvailabilityDto;
  /** Newest first; the first one is the current session. */
  sessions: AgentSessionDto[];
}

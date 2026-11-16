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
  /** A sentence for a person when the connection's newest delivery failed; null otherwise. */
  warning: string | null;
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

export const AGENT_END_REASONS = [
  'completed',
  'canceled',
  'deliveryFailed',
  'agentError',
  'stale',
  'proposalWithoutIssue',
  'unknown',
] as const;
export type AgentEndReason = (typeof AGENT_END_REASONS)[number];

/** One send of a delivery to the agent connection, oldest first. Never carries the delivery body. */
export interface AgentDeliveryAttempt {
  action: string;
  attempt: number;
  at: string;
  atLabel: string;
  /** Null when no answer came. */
  httpStatus: number | null;
  /** Already scrubbed by the server; null on success. */
  error: string | null;
  durationMs: number;
  nextAttemptAt: string | null;
  nextAttemptLabel: string | null;
}

/** The agent's own output, left on the item as an attachment. Project admins only. */
export interface AgentRunLog {
  attachmentId: string;
  name: string;
  sizeBytes: number;
  /** The attachment download route. */
  url: string;
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
  /** Set for a session that is not live (ended or stale); null while live. */
  endReason: AgentEndReason | null;
  /** The sentence for a person; every project member sees it. */
  endText: string | null;
  /** Null for a member when the reason is deliveryFailed. */
  endDetail: string | null;
  /** Null for a member and when the agent left none. */
  runLog: AgentRunLog | null;
  /** Oldest first. Empty for a member. */
  deliveries: AgentDeliveryAttempt[];
}

export interface AgentPanelDto {
  /** False when the item has no session and the project has no agent connection: show nothing. */
  visible: boolean;
  availability: AgentStartAvailabilityDto;
  /** Newest first; the first one is the current session. */
  sessions: AgentSessionDto[];
}

import type {
  AgentActivityDto,
  AgentDeliveryAttempt,
  AgentPanelDto,
  AgentRunLog,
  AgentSessionDto,
  AgentStartAvailabilityDto,
} from '@shiplean/core/api/agentSessions';

// Fixtures for the agent panel's end reasons, delivery attempts, run log and start warning. Text follows the C2 sentences;
// the real ones come from the server (AgentEndText.For), so a spec asserts what it is given, not a rewording of it.

export const activity = (
  id: string,
  type: AgentActivityDto['type'],
  body: string | null,
  extra: Partial<AgentActivityDto> = {},
): AgentActivityDto => ({
  id,
  type,
  body,
  action: null,
  parameter: null,
  result: null,
  ephemeral: false,
  signal: null,
  createdAt: '2026-11-15T09:00:00+00:00',
  createdLabel: '5 minutes ago',
  ...extra,
});

/** An ended session with no end information yet; each fixture below overrides what it needs. */
export const endedSession = (
  id: string,
  extra: Partial<AgentSessionDto> = {},
): AgentSessionDto => ({
  id,
  connectionName: 'Relay',
  state: 'completed',
  live: false,
  stopRequested: false,
  startedAt: '2026-11-15T08:50:00+00:00',
  startedLabel: '15 minutes ago',
  lastActivityAt: '2026-11-15T09:00:00+00:00',
  lastActivityLabel: '5 minutes ago',
  endedLabel: '5 minutes ago',
  urls: [],
  plan: [],
  activities: [],
  endReason: null,
  endText: null,
  endDetail: null,
  runLog: null,
  deliveries: [],
  ...extra,
});

export const twoAttempts: AgentDeliveryAttempt[] = [
  {
    action: 'created',
    attempt: 1,
    at: '2026-11-15T08:50:05+00:00',
    atLabel: '15 minutes ago',
    httpStatus: null,
    error: 'No answer within 5 seconds.',
    durationMs: 5000,
    nextAttemptAt: '2026-11-15T08:50:35+00:00',
    nextAttemptLabel: '14 minutes ago',
  },
  {
    action: 'created',
    attempt: 2,
    at: '2026-11-15T08:50:35+00:00',
    atLabel: '14 minutes ago',
    httpStatus: 503,
    error: 'The receiver answered 503.',
    durationMs: 120,
    nextAttemptAt: null,
    nextAttemptLabel: null,
  },
];

export const runLog: AgentRunLog = {
  attachmentId: '5d1c1b0e-0000-4000-8000-000000000001',
  name: 'agent-run-s-error.log',
  sizeBytes: 18432,
  url: '/api/attachments/5d1c1b0e-0000-4000-8000-000000000001',
};

export const completed: AgentSessionDto = endedSession('s-completed', {
  state: 'completed',
  endReason: 'completed',
  endText: 'The agent finished.',
});

/** What a project admin gets: the detail and both attempts. */
export const deliveryFailedAsAdmin: AgentSessionDto = endedSession('s-delivery', {
  state: 'failed',
  endReason: 'deliveryFailed',
  endText: "The agent connection didn't take the job: The receiver answered 503. (2 tries)",
  endDetail: 'The receiver answered 503. (2 tries)',
  deliveries: twoAttempts,
});

/** The same session as a project member sees it: no detail, no attempts. */
export const deliveryFailedAsMember: AgentSessionDto = {
  ...deliveryFailedAsAdmin,
  id: 's-delivery-member',
  endText: "The agent connection didn't take the job",
  endDetail: null,
  deliveries: [],
};

export const agentError: AgentSessionDto = endedSession('s-error', {
  state: 'failed',
  endReason: 'agentError',
  endText: 'The agent reported an error: The build failed.',
  endDetail: 'The build failed.\nexit code 1',
  runLog,
  activities: [activity('a-error', 'error', 'The build failed.\nexit code 1')],
});

export const stale: AgentSessionDto = endedSession('s-stale', {
  state: 'stale',
  endedLabel: null,
  endReason: 'stale',
  endText: 'The agent went quiet for 30 minutes.',
  endDetail: 'No activity for 30 minutes since 2026-11-15T09:00:00Z.',
});

export const unknownReason: AgentSessionDto = endedSession('s-unknown', {
  state: 'failed',
  endReason: 'unknown',
  endText: 'The agent session ended before ShipLean recorded why.',
});

export const canStart: AgentStartAvailabilityDto = {
  canStart: true,
  reason: null,
  message: '',
  warning: null,
};

export const canStartWithWarning: AgentStartAvailabilityDto = {
  ...canStart,
  warning: 'The last delivery to this connection failed 10 minutes ago.',
};

export const panelWith = (
  sessions: AgentSessionDto[],
  availability: AgentStartAvailabilityDto = canStart,
): AgentPanelDto => ({ visible: true, availability, sessions });

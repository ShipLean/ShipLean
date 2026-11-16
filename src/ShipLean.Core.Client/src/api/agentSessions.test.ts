import { expect, test } from 'vitest';
import type { AgentPanelDto } from './agentSessions';

// Typecheck fails if a fixture lacks a field, has one of the wrong type, or has one the type doesn't declare.
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

// The wire shape of C4: camelCase, nothing omitted, absent values null.
const panel = {
  visible: true,
  availability: {
    canStart: true,
    reason: null,
    message: '',
    warning: 'The last delivery to this connection failed 10 minutes ago.',
  },
  sessions: [
    {
      id: 's-1',
      connectionName: 'Relay',
      state: 'failed',
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
      endReason: 'deliveryFailed',
      endText: "The agent connection didn't take the job: The receiver answered 503. (2 tries)",
      endDetail: 'The receiver answered 503. (2 tries)',
      runLog: {
        attachmentId: '5d1c1b0e-0000-4000-8000-000000000001',
        name: 'agent-run-s-1.log',
        sizeBytes: 18432,
        url: '/api/attachments/5d1c1b0e-0000-4000-8000-000000000001',
      },
      deliveries: [
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
      ],
    },
  ],
};

test('AgentPanelDto_WhenItCarriesTheEndFields_ShouldParse', () => {
  const parsed = typed<AgentPanelDto>()(JSON.parse(JSON.stringify(panel)) as typeof panel);
  const session = parsed.sessions[0];

  expect(session.endReason).toBe('deliveryFailed');
  expect(session.endText).toContain("didn't take the job");
  expect(session.endDetail).toBe('The receiver answered 503. (2 tries)');
  expect(session.runLog?.name).toBe('agent-run-s-1.log');
  expect(session.deliveries.map((attempt) => attempt.attempt)).toEqual([1, 2]);
  expect(session.deliveries[1].nextAttemptAt).toBeNull();
  expect(parsed.availability.warning).toContain('failed');
});

import type { AgentConnectionHealth, AgentConnectionTest } from '../model';

export const healthyConnection: AgentConnectionHealth = {
  lastDeliveredAt: '2026-11-15T09:00:00Z',
  lastDeliveredLabel: '5 minutes ago',
  failedLast24h: 0,
  lastFailedAt: null,
  lastFailedLabel: null,
  lastError: null,
};

export const failingConnection: AgentConnectionHealth = {
  lastDeliveredAt: '2026-11-14T18:00:00Z',
  lastDeliveredLabel: 'yesterday',
  failedLast24h: 3,
  lastFailedAt: '2026-11-15T08:55:00Z',
  lastFailedLabel: '10 minutes ago',
  lastError: 'No answer within 5 seconds.',
};

export const testReached: AgentConnectionTest = {
  reached: true,
  httpStatus: 400,
  error: null,
  durationMs: 85,
};

export const testUnreachable: AgentConnectionTest = {
  reached: false,
  httpStatus: null,
  error: 'No answer within 5 seconds.',
  durationMs: 5000,
};

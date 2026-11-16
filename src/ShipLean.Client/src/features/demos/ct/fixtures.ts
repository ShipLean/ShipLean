import type { SprintDemoActionsDto } from '@shiplean/core/api/demos';

export const createWarning = 'The last delivery to this connection failed 10 minutes ago.';

/** Create demo is on, and the connection's newest delivery failed. */
export const actionsWithWarning: SprintDemoActionsDto = {
  canCreate: true,
  createBlockedMessage: null,
  createWarning,
  canRetryYouTube: false,
};

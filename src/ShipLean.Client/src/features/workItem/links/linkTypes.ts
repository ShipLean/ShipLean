import type { LinkType } from '@shiplean/core/api/persisted';

export const linkTypeLabels: Record<LinkType, string> = {
  predecessor: 'Predecessor',
  successor: 'Successor',
  blockedBy: 'Blocked by',
  related: 'Related',
  duplicate: 'Duplicate',
  url: 'External URL',
  // TODO: offered for any item pair, and the server accepts it on any pair; the Bug-to-Task rule in Features/Links replaces it.
  causedBy: 'Caused by',
};

const WEB_URL = /^https?:\/\/[^\s/]+\.[^\s]+$/;

/** Whether the text is a full http(s) address, the only kind a link may carry. */
export const isWebUrl = (text: string): boolean => WEB_URL.test(text);

export const actionsHarnessIds = {
  location: 'ct-actions-location',
  toasts: 'ct-actions-toasts',
  pick: (id: string) => `ct-actions-pick-${id}`,
  detail: (id: string) => `ct-actions-detail-${id}`,
} as const;

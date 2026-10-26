export const actionsHarnessIds = {
  location: 'ct-actions-location',
  toasts: 'ct-actions-toasts',
  pick: (id: string) => `ct-actions-pick-${id}`,
} as const;

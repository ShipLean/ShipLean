export const firstResultSheetIds = {
  root: 'sheet-first-result',
  lead: 'sheet-first-result-lead',
  rows: 'sheet-first-result-rows',
  unchanged: 'sheet-first-result-unchanged',
  next: 'sheet-first-result-next',
  openInbox: 'sheet-first-result-open-inbox',
  done: 'sheet-first-result-done',
} as const;

export const firstResultRowId = (key: string) => `sheet-first-result-row-${key}`;

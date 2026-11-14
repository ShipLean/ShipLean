// CT-only ids for the SaveBar and full-screen Dialog harnesses. Not pinned: src/touch/testIds.ts holds the shared ids.
export const saveBarHarnessIds = {
  ctSaveBarPageEnd: 'ct-save-bar-page-end',
  ctSaveBarSaves: 'ct-save-bar-saves',
  ctSaveBarDiscards: 'ct-save-bar-discards',
  ctSaveBarMessageButton: 'ct-save-bar-message-button',
  ctSaveBarMessageClicks: 'ct-save-bar-message-clicks',
  ctDialogOpener: 'ct-full-screen-dialog-opener',
  ctDialogPrimaryButton: 'ct-full-screen-dialog-primary-button',
  ctDialogBodyEnd: 'ct-full-screen-dialog-body-end',
  ctDialogOverflowChild: 'ct-full-screen-dialog-overflow-child',
  ctDialogState: 'ct-full-screen-dialog-state',
} as const;

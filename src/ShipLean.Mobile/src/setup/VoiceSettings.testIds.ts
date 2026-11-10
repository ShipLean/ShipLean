export const voiceSettingsIds = {
  root: 'settings-voice',
  status: 'settings-voice-status',
  setUp: 'settings-voice-set-up',
  commandModel: 'settings-voice-command-model',
  // The progress of a model change, under the command model row.
  change: 'settings-voice-change',
  changeBar: 'settings-voice-change-bar',
  changeRetry: 'settings-voice-change-retry',
  deleteError: 'settings-voice-delete-error',
  speech: 'settings-voice-speech',
  tryOther: 'settings-voice-try-other',
  wifiOnly: 'settings-voice-wifi-only',
  storageTotal: 'settings-voice-storage-total',
  deleteModels: 'settings-voice-delete',
  deleteSheet: 'sheet-settings-voice-delete',
  deleteLine: 'settings-voice-delete-line',
  deleteConfirm: 'settings-voice-delete-confirm',
  deleteCancel: 'settings-voice-delete-cancel',
  microphone: 'settings-voice-microphone',
  microphoneValue: 'settings-voice-microphone-value',
  licences: 'settings-voice-licences',
} as const;

export const storageRowId = (model: string) => `settings-voice-storage-${model}`;

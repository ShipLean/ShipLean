export const downloadingStepIds = {
  root: 'setup-downloading',
  percent: 'setup-downloading-percent',
  bar: 'setup-downloading-bar',
  amount: 'setup-downloading-amount',
  // The one line saying why it is waiting or stopped.
  status: 'setup-downloading-status',
  files: 'setup-downloading-files',
  promises: 'setup-downloading-promises',
  keepOpen: 'setup-downloading-keep-open',
  // The microphone card after a refusal, and the quieter line after Not now.
  micOff: 'setup-downloading-mic-off',
  micOffSettings: 'setup-downloading-mic-off-settings',
  micNotAsked: 'setup-downloading-mic-not-asked',
  // Not enough space.
  noSpace: 'setup-downloading-no-space',
  noSpaceBody: 'setup-downloading-no-space-body',
  tryAgain: 'setup-downloading-try-again',
  openStorage: 'setup-downloading-open-storage',
  useSmaller: 'setup-downloading-use-smaller',
  // A download that failed for another reason (checksum, network).
  failedTryAgain: 'setup-downloading-failed-try-again',
  // Nothing is downloading.
  idle: 'setup-downloading-idle',
  setUp: 'setup-downloading-set-up',
  useApp: 'setup-downloading-use-app',
  pause: 'setup-downloading-pause',
  resume: 'setup-downloading-resume',
} as const;

export const downloadFileRowId = (model: string) => `setup-downloading-file-${model}`;

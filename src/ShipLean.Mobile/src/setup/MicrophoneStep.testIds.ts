export const microphoneStepIds = {
  root: 'setup-microphone',
  // The thin line at the top: "Voice models downloading · 4%".
  downloadLine: 'setup-microphone-download-line',
  promises: 'setup-microphone-promises',
  privacy: 'setup-microphone-promises-privacy',
  next: 'setup-microphone-next',
  continue: 'setup-microphone-continue',
  notNow: 'setup-microphone-not-now',
} as const;

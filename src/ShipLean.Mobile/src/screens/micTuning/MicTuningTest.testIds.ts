export const micTuningTestId = 'mic-tuning-test';

export const micTuningTestIds = {
  start: 'mic-tuning-test-start',
  startButton: 'mic-tuning-test-start-button',
  notReady: 'mic-tuning-test-not-ready',
  stepQuiet: 'mic-tuning-test-step-quiet',
  countdown: 'mic-tuning-test-countdown',
  stepSpeak: 'mic-tuning-test-step-speak',
  phrase: 'mic-tuning-test-phrase',
  partial: 'mic-tuning-test-partial',
  hold: 'mic-tuning-test-hold',
  meter: 'mic-tuning-test-meter',
  resultGood: 'mic-tuning-test-result-good',
  resultTooQuiet: 'mic-tuning-test-result-too-quiet',
  resultNoisy: 'mic-tuning-test-result-noisy',
  resultNoSignal: 'mic-tuning-test-result-no-signal',
  heard: 'mic-tuning-test-heard',
  sensitivity: 'mic-tuning-test-sensitivity',
  cause: (n: number) => `mic-tuning-test-cause-${n}`,
  tryAgain: 'mic-tuning-test-try-again',
  typeInstead: 'mic-tuning-test-type-instead',
} as const;

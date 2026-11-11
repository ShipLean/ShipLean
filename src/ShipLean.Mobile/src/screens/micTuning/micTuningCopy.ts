// The words on the test and its results, in one place.
import type { TuningResultKind } from '../../voice/micTuning/line';

export const NO_KEY_PHRASE = 'Show my inbox';
export const phraseFor = (key: string | undefined): string =>
  key === undefined ? NO_KEY_PHRASE : `Move ${key} to Done`;
export const hotwordsFor = (key: string | undefined): string[] =>
  key === undefined ? ['Show', 'inbox'] : [key, 'Move', 'Done'];

export const QUIET_SECONDS = 3;

export const copy = {
  startTitle: 'Test your mic',
  startBody: 'Two steps, about 10 seconds. First stay quiet, then say a phrase.',
  startButton: 'Start the test',
  typeInstead: 'Type instead',
  tryAgain: 'Try again',
  notReady: "Voice isn't ready. Finish setting up voice, then test again.",
  quietTitle: 'Stay quiet',
  quietStep: 'Step 1 of 2 · measuring the room',
  quietLeft: (s: number) => `${s} s`,
  speakTitle: 'Hold the mic and say',
  speakStep: 'Step 2 of 2 · at your usual distance',
  speakRead: 'Read this aloud',
  holdHint: 'Hold to talk',
  holdRelease: "Release when you've finished",
  heard: 'Heard',
  nothingHeard: 'Nothing was transcribed',
  sensitivity: (n: number) => `Sensitivity Automatic ${n} of 10`,
} as const;

export const resultCopy: Record<TuningResultKind, { title: string; body: string }> = {
  good: { title: 'Your mic is working', body: 'The line is set from your voice and this room.' },
  tooQuiet: {
    title: 'Your voice was coming through quietly',
    body: 'Too quiet for the old setting, so voice said it heard nothing. The line is lower now.',
  },
  noisy: {
    title: 'The room is too noisy',
    body: 'The room is almost as loud as your voice. Move closer to the mic and away from the noise, then test again.',
  },
  noSignal: {
    title: 'No sound is reaching ShipLean',
    body: 'Not even the room: something on the phone is stopping the microphone.',
  },
};

export interface Cause {
  title: string;
  body: string;
}

export const noSignalCauses: Record<'android' | 'ios', Cause[]> = {
  android: [
    {
      title: 'Microphone access is off',
      body: 'Swipe down from the top. In quick settings, turn on Microphone access.',
    },
    {
      title: 'Another app has the microphone',
      body: 'A call, a voice recorder or a video app. Close it and try again.',
    },
    {
      title: 'Something covers the mic',
      body: "It's the small hole at the bottom edge. A case or a finger can block it.",
    },
  ],
  ios: [
    {
      title: 'Another app has the microphone',
      body: 'A call, a voice recorder or a video app. Close it and try again.',
    },
    {
      title: 'Something covers the mic',
      body: "It's the small hole at the bottom edge. A case or a finger can block it.",
    },
    {
      title: 'Screen Time blocks it',
      body: 'In Settings, open Screen Time, then Content & Privacy Restrictions, and allow Microphone.',
    },
  ],
};

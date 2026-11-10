// The voice engines the provider uses. The app configures the native ones at start-up; tests configure fakes.
import type { ModelStatus, VoiceEngines } from './contracts';

const NO_MODELS: ModelStatus = {
  phase: 'none',
  choice: 'auto',
  resolved: null,
  bytesDone: 0,
  bytesTotal: 0,
  freeBytes: 0,
  wifiOnly: true,
  reason: null,
  files: [],
};

// Engines that aren't ready: no recognizer or model, no models downloaded, and the mic never asked.
export function notReadyEngines(): VoiceEngines {
  return {
    recognizer: {
      ready: () => false,
      inputName: () => null,
      start: () => Promise.reject(new Error('The speech recognizer is not configured.')),
    },
    model: {
      ready: () => false,
      prepare: () => Promise.reject(new Error('The command model is not configured.')),
      parse: () => Promise.reject(new Error('The command model is not configured.')),
    },
    models: {
      status: () => NO_MODELS,
      subscribe: () => () => {},
      choose: () => {},
      download: () => {},
      pause: () => {},
      useMobileDataOnce: () => {},
      skip: () => {},
      deleteAll: () => Promise.resolve(),
      deviceMemory: () => ({ reportedGb: 0, soldGb: 0 }),
      reportLoadFailure: () => {},
    },
    mic: {
      status: () => Promise.resolve('undetermined'),
      request: () => Promise.resolve(false),
      openSettings: () => {},
    },
  };
}

let current: VoiceEngines = notReadyEngines();

// Replaces the engines every provider mounted after this reads.
export function configureEngines(e: VoiceEngines): void {
  current = e;
}

export function engines(): VoiceEngines {
  return current;
}

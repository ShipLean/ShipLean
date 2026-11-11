import { PHONE_INPUT_ID, type MicTuning, type MicTuningStore } from '../contracts';

// TODO: persist to the kv store under voice.micTuning as { picked, tunings }. For now the state is in memory and the
// storage is not read.
export function createMicTuningStore(_storage: {
  getItemSync(k: string): string | null;
  setItemSync(k: string, v: string): void;
}): MicTuningStore {
  let picked = PHONE_INPUT_ID;
  const tunings = new Map<string, MicTuning>();
  const listeners = new Set<() => void>();
  const notify = () => {
    for (const cb of [...listeners]) cb();
  };
  return {
    pickedInput: () => picked,
    pick(inputId) {
      picked = inputId;
      notify();
    },
    tuning: (inputId) => tunings.get(inputId) ?? null,
    save(t) {
      tunings.set(t.inputId, t);
      notify();
    },
    subscribe(cb) {
      listeners.add(cb);
      return () => {
        listeners.delete(cb);
      };
    },
  };
}

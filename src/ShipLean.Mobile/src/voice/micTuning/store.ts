import { PHONE_INPUT_ID, type MicTuning, type MicTuningStore } from '../contracts';

const KEY = 'voice.micTuning';

interface Persisted {
  picked: string;
  tunings: Record<string, MicTuning>;
}

const EMPTY: Persisted = { picked: PHONE_INPUT_ID, tunings: {} };

// Corrupt or foreign JSON reads as empty; it never throws.
function read(raw: string | null): Persisted {
  if (raw === null) return EMPTY;
  try {
    const parsed: unknown = JSON.parse(raw);
    if (typeof parsed !== 'object' || parsed === null) return EMPTY;
    const { picked, tunings } = parsed as Partial<Persisted>;
    return {
      picked: typeof picked === 'string' && picked !== '' ? picked : PHONE_INPUT_ID,
      tunings:
        typeof tunings === 'object' && tunings !== null && !Array.isArray(tunings) ? tunings : {},
    };
  } catch {
    return EMPTY;
  }
}

export function createMicTuningStore(storage: {
  getItemSync(k: string): string | null;
  setItemSync(k: string, v: string): void;
}): MicTuningStore {
  const listeners = new Set<() => void>();
  const load = () => read(storage.getItemSync(KEY));
  const write = (next: Persisted) => {
    storage.setItemSync(KEY, JSON.stringify(next));
    for (const cb of [...listeners]) cb();
  };
  return {
    pickedInput: () => load().picked,
    pick: (inputId) => write({ ...load(), picked: inputId }),
    tuning: (inputId) => load().tunings[inputId] ?? null,
    save(t) {
      const current = load();
      write({ ...current, tunings: { ...current.tunings, [t.inputId]: t } });
    },
    subscribe(cb) {
      listeners.add(cb);
      return () => {
        listeners.delete(cb);
      };
    },
  };
}

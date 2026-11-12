import { useEffect, useState } from 'react';
import type { AudioInput, MicTuning } from '../../../voice/contracts';
import { engines } from '../../../voice/engines';
import { resolveInput } from '../../../voice/micTuning/route';

// Re-renders the caller whenever an input connects or disconnects, or the tuning store changes.
export function useVoiceInputs(): {
  inputs: AudioInput[];
  picked: string;
  inUse: AudioInput;
  tuningOf: (inputId: string) => MicTuning | null;
} {
  const { inputs, tuning } = engines();
  const [, setTick] = useState(0);
  useEffect(() => {
    const bump = () => setTick((n) => n + 1);
    const offInputs = inputs.subscribe(bump);
    const offTuning = tuning.subscribe(bump);
    return () => {
      offInputs();
      offTuning();
    };
  }, [inputs, tuning]);
  const list = inputs.list();
  const picked = tuning.pickedInput();
  return { inputs: list, picked, inUse: resolveInput(picked, list), tuningOf: tuning.tuning };
}

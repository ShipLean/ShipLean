// The app's voice engines from the native adapters (N9): the model store first, then the recognizer and the command
// model over it, so both load and free their models as the store's phase changes. Pure: no native imports, so it runs
// under vitest with fake adapters. index.ts calls it once, before the first render.
import type { KeyValueStore } from '@shiplean/core/platform';
import { createModelStore } from '../../setup/modelStore';
import type { VoiceEngines } from '../contracts';
import { createMicTuningStore } from '../micTuning/store';
import type { ModelFile } from '../manifest';
import type { NativeAdapters } from '../native';

export interface BuildEnginesOptions {
  // Per phone, not per account: the model choice and the guide flags survive sign-out.
  kv: KeyValueStore;
  manifest: readonly ModelFile[];
  clock: () => Date;
}

export function buildEngines(
  adapters: NativeAdapters,
  { kv, manifest, clock }: BuildEnginesOptions,
): VoiceEngines {
  const { files, network, memoryBytes } = adapters;
  const models = createModelStore({ files, network, kv, memoryBytes, manifest, clock });
  // TODO: the kv store behind the tuning store, once it persists.
  const tuning = createMicTuningStore({ getItemSync: () => null, setItemSync: () => {} });
  const { inputs } = adapters;
  return {
    recognizer: adapters.recognizer({ files, models, inputs, tuning }),
    model: adapters.model({ files, models }),
    models,
    mic: adapters.mic,
    inputs,
    tuning,
  };
}

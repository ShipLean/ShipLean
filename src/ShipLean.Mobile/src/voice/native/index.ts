// The native side of N9. The recognizer and the command model follow the model store, so they come as factories that
// buildEngines calls with the files and the store it made from the other adapters.
import type { CommandModel, MicPermission, SpeechRecognizer } from '../contracts';
import { LlamaCommandModel, type CommandModelDeps } from './commandModel';
import { deviceMemoryBytes } from './deviceMemory';
import { ExpoMicPermission } from './micPermission';
import { ExpoModelFiles } from './modelFiles';
import { NetInfoNetworkKind } from './network';
import type { ModelFiles, NetworkKind } from './ports';
import { NativeSpeechRecognizer, type SpeechRecognizerDeps } from './speechRecognizer';

export interface NativeAdapters {
  recognizer: (d: SpeechRecognizerDeps) => SpeechRecognizer;
  model: (d: CommandModelDeps) => CommandModel;
  mic: MicPermission;
  files: ModelFiles;
  network: NetworkKind;
  memoryBytes: number;
}

export function nativeAdapters(): NativeAdapters {
  return {
    recognizer: (d) => new NativeSpeechRecognizer(d),
    model: (d) => new LlamaCommandModel(d),
    mic: new ExpoMicPermission(),
    files: new ExpoModelFiles(),
    network: new NetInfoNetworkKind(),
    memoryBytes: deviceMemoryBytes(),
  };
}

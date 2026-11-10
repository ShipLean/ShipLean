// The native side of N9, one instance of each adapter. VW wires them into configureEngines with the model store.
import type { CommandModel, MicPermission, SpeechRecognizer } from '../contracts';
import { LlamaCommandModel } from './commandModel';
import { deviceMemoryBytes } from './deviceMemory';
import { ExpoMicPermission } from './micPermission';
import { ExpoModelFiles } from './modelFiles';
import { NetInfoNetworkKind } from './network';
import type { ModelFiles, NetworkKind } from './ports';
import { NativeSpeechRecognizer } from './speechRecognizer';

export interface NativeAdapters {
  recognizer: SpeechRecognizer;
  model: CommandModel;
  mic: MicPermission;
  files: ModelFiles;
  network: NetworkKind;
  memoryBytes: number;
}

export function nativeAdapters(): NativeAdapters {
  return {
    recognizer: new NativeSpeechRecognizer(),
    model: new LlamaCommandModel(),
    mic: new ExpoMicPermission(),
    files: new ExpoModelFiles(),
    network: new NetInfoNetworkKind(),
    memoryBytes: deviceMemoryBytes(),
  };
}

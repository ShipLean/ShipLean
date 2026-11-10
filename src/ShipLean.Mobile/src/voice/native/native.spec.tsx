import type { CommandModel, MicPermission, SpeechRecognizer } from '../contracts';
import { setTotalMemory } from '../../test-support/nativeMocks';
import { LlamaCommandModel } from './commandModel';
import { deviceMemoryBytes } from './deviceMemory';
import { nativeAdapters } from './index';
import { ExpoMicPermission } from './micPermission';
import { ExpoModelFiles } from './modelFiles';
import { NetInfoNetworkKind } from './network';
import type { ModelFiles, NetworkKind } from './ports';
import { NativeSpeechRecognizer } from './speechRecognizer';

describe('native adapters', () => {
  it('each adapter implements its N9 interface and its stub says it is not ready', () => {
    // Typed as the interfaces, so a missing or mistyped method fails the typecheck.
    const recognizer: SpeechRecognizer = new NativeSpeechRecognizer();
    const model: CommandModel = new LlamaCommandModel();
    const mic: MicPermission = new ExpoMicPermission();
    const files: ModelFiles = new ExpoModelFiles();
    const network: NetworkKind = new NetInfoNetworkKind();

    expect(recognizer.ready()).toBe(false);
    expect(model.ready()).toBe(false);
    for (const [adapter, methods] of [
      [recognizer, ['ready', 'inputName', 'start']],
      [model, ['ready', 'prepare', 'parse']],
      [mic, ['status', 'request', 'openSettings']],
      [files, ['freeBytes', 'bytesOnDisk', 'sha256', 'remove', 'path', 'download']],
      [network, ['kind', 'subscribe']],
    ] as const) {
      for (const method of methods) {
        expect(typeof (adapter as unknown as Record<string, unknown>)[method]).toBe('function');
      }
    }
  });

  it('deviceMemoryBytes reads expo-device and is 0 when unknown', () => {
    setTotalMemory(11_811_160_064);
    expect(deviceMemoryBytes()).toBe(11_811_160_064);

    setTotalMemory(null);
    expect(deviceMemoryBytes()).toBe(0);
  });

  it('nativeAdapters returns every port', () => {
    setTotalMemory(8 * 1024 ** 3);

    const adapters = nativeAdapters();

    expect(adapters.recognizer).toBeInstanceOf(NativeSpeechRecognizer);
    expect(adapters.model).toBeInstanceOf(LlamaCommandModel);
    expect(adapters.mic).toBeInstanceOf(ExpoMicPermission);
    expect(adapters.files).toBeInstanceOf(ExpoModelFiles);
    expect(adapters.network).toBeInstanceOf(NetInfoNetworkKind);
    expect(adapters.memoryBytes).toBe(8 * 1024 ** 3);
    expect(Object.keys(adapters).sort()).toEqual(
      ['files', 'memoryBytes', 'mic', 'model', 'network', 'recognizer'].sort(),
    );
  });
});

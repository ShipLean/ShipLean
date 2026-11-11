import { memoryStore } from '@shiplean/core/platform';
import { describe, expect, it } from 'vitest';
import { FakeModelFiles, FakeNetwork, sizedManifest } from '../../test-support/modelFiles';
import { FakeCommandModel, FakeMicPermission, FakeRecognizer } from '../../test-support/voice';
import type { CommandModelDeps } from '../native/commandModel';
import type { NativeAdapters } from '../native';
import type { SpeechRecognizerDeps } from '../native/speechRecognizer';
import { buildEngines } from './buildEngines';

const GIB = 2 ** 30;

describe('buildEngines', () => {
  it('builds the engines from the native adapters and the model store', () => {
    const files = new FakeModelFiles();
    const network = new FakeNetwork('wifi');
    const mic = new FakeMicPermission();
    const recognizer = new FakeRecognizer();
    const model = new FakeCommandModel();
    const recognizerDeps: SpeechRecognizerDeps[] = [];
    const modelDeps: CommandModelDeps[] = [];
    const adapters: NativeAdapters = {
      recognizer: (d) => {
        recognizerDeps.push(d);
        return recognizer;
      },
      model: (d) => {
        modelDeps.push(d);
        return model;
      },
      mic,
      files,
      network,
      memoryBytes: 7.4 * GIB,
      keepAlive: { start: () => true, stop: () => undefined },
    };
    const kv = memoryStore();

    const engines = buildEngines(adapters, {
      kv,
      manifest: sizedManifest(),
      clock: () => new Date(0),
    });

    // The store is the one built from the adapters: the phone's memory, and the per-phone settings in kv.
    expect(engines.models.deviceMemory().reportedGb).toBe(7.4);
    engines.models.setWifiOnly(false);
    expect(engines.models.status().wifiOnly).toBe(false);
    expect(kv.getItem('shiplean.voice.wifiOnly')).toBe('false');
    // The recognizer and the model each come from their factory, once, over the same files and that same store.
    expect(recognizerDeps).toHaveLength(1);
    expect(modelDeps).toHaveLength(1);
    expect(recognizerDeps[0].files).toBe(files);
    expect(recognizerDeps[0].models).toBe(engines.models);
    expect(modelDeps[0].files).toBe(files);
    expect(modelDeps[0].models).toBe(engines.models);
    expect(engines.recognizer).toBe(recognizer);
    expect(engines.model).toBe(model);
    expect(engines.mic).toBe(mic);
  });
});

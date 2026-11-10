import { memoryStore } from '@shiplean/core/platform';
import { describe, expect, it } from 'vitest';
import { FakeModelFiles, FakeNetwork, sizedManifest } from '../test-support/modelFiles';
import {
  DEFAULT_MODEL_SETTINGS,
  loadModelSettings,
  MODEL_SETTINGS_KEYS,
  saveModelSettings,
} from './modelSettings';
import { createModelStore } from './modelStore';

describe('modelSettings', () => {
  it('Wi-Fi only defaults to on and survives a reload', () => {
    const kv = memoryStore();
    expect(loadModelSettings(kv).wifiOnly).toBe(true);

    saveModelSettings(kv, { ...loadModelSettings(kv), wifiOnly: false });
    expect(loadModelSettings(kv).wifiOnly).toBe(false);

    const manifest = sizedManifest();
    const store = createModelStore({
      files: new FakeModelFiles(manifest),
      network: new FakeNetwork(),
      kv,
      memoryBytes: 8 * 1024 ** 3,
      manifest,
      clock: () => new Date(0),
    });
    expect(store.status().wifiOnly).toBe(false);
    store.setWifiOnly(true);
    expect(loadModelSettings(kv).wifiOnly).toBe(true);
  });

  it('saves and loads the choice, the file states and the download under way', () => {
    const kv = memoryStore();
    const saved = {
      choice: 'qwen3-4b' as const,
      wifiOnly: true,
      skipped: false,
      inUse: 'qwen3-1.7b' as const,
      files: { a: 'checked' as const, b: 'downloading' as const },
      download: { startedAt: 42, mobileDataOnce: true, paused: false },
    };
    saveModelSettings(kv, saved);
    expect(loadModelSettings(kv)).toEqual(saved);

    saveModelSettings(kv, DEFAULT_MODEL_SETTINGS);
    expect(kv.getItem(MODEL_SETTINGS_KEYS.download)).toBeNull();
    expect(loadModelSettings(kv)).toEqual(DEFAULT_MODEL_SETTINGS);
  });

  it('ignores values it does not know', () => {
    const kv = memoryStore();
    kv.setItem(MODEL_SETTINGS_KEYS.choice, 'qwen3-70b');
    kv.setItem(MODEL_SETTINGS_KEYS.files, '{"a":"done","b":"checked"}');
    kv.setItem(MODEL_SETTINGS_KEYS.download, 'not json');
    expect(loadModelSettings(kv)).toEqual({ ...DEFAULT_MODEL_SETTINGS, files: { b: 'checked' } });
  });
});

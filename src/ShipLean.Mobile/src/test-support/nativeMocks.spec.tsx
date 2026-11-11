import { renderHook, screen } from '@testing-library/react-native';
import * as expoAudio from 'expo-audio';
import * as expoDevice from 'expo-device';
import * as expoFileSystem from 'expo-file-system';
import * as expoFileSystemLegacy from 'expo-file-system/legacy';
import * as llamaRn from 'llama.rn';
import * as sherpaStt from 'react-native-sherpa-onnx/stt';
import { screenIds } from '../screens/testIds';
import { nativeAdapters } from '../voice/native';
import {
  emitAudio,
  native,
  resetNativeMocks,
  setMicPermission,
  setTotalMemory,
} from './nativeMocks';
import { renderApp } from './renderApp';

// Every native module the voice code binds, as the app imports it. Without its mock in jest.setup.ts the app would get
// the real module, which needs a native side jest doesn't have.
const NATIVE_MODULES: [string, unknown][] = [
  ['llama.rn', llamaRn],
  ['react-native-sherpa-onnx/stt', sherpaStt],
  ['expo-audio', expoAudio],
  ['expo-file-system', expoFileSystem],
  ['expo-file-system/legacy', expoFileSystemLegacy],
  ['expo-device', expoDevice],
];

describe('native mocks', () => {
  it('mocks every native module the app imports, so a screen renders under jest', async () => {
    setTotalMemory(6 * 1024 ** 3);
    setMicPermission({ status: 'granted', granted: true, canAskAgain: true });

    await renderApp();
    expect(await screen.findByTestId(screenIds.projects)).toBeOnTheScreen();
    expect(nativeAdapters().memoryBytes).toBe(6 * 1024 ** 3);

    for (const [name, imported] of NATIVE_MODULES) {
      expect([name, imported]).toEqual([name, jest.requireMock(name)]);
    }

    const llama = jest.requireMock<typeof import('llama.rn')>('llama.rn');
    const context = await llama.initLlama({ model: 'file:///document/models/qwen3.gguf' });
    expect(native.llamaInits).toEqual([{ model: 'file:///document/models/qwen3.gguf' }]);
    expect(context.release).toBeDefined();

    const audio = jest.requireMock<typeof import('expo-audio')>('expo-audio');
    expect(await audio.getRecordingPermissionsAsync()).toMatchObject({
      status: 'granted',
      granted: true,
    });
    const stream = new audio.AudioModule.AudioStream({
      sampleRate: 16000,
      channels: 1,
      encoding: 'int16',
    });
    await stream.start();
    await stream.stop();
    expect(native.streamCalls).toEqual([
      { call: 'create', sampleRate: 16000, channels: 1, encoding: 'int16' },
      { call: 'start' },
      { call: 'stop' },
    ]);

    const legacy =
      jest.requireMock<typeof import('expo-file-system/legacy')>('expo-file-system/legacy');
    const download = legacy.createDownloadResumable(
      'https://example.test/m.gguf',
      'file:///document/m.gguf',
    );
    const done = download.downloadAsync();
    native.downloads[0]!.finish({ status: 200 }, 'model');
    await expect(done).resolves.toMatchObject({ status: 200 });
    expect(native.files.get('file:///document/m.gguf')).toBe('model');
  });

  it('records one stream per mounted hook, however often it re-renders', async () => {
    const audio = jest.requireMock<typeof import('expo-audio')>('expo-audio');
    const { result, rerender } = await renderHook(() =>
      audio.useAudioStream({ sampleRate: 16000 }),
    );
    const first = result.current.stream;
    await rerender({});
    await rerender({});
    expect(result.current.stream).toBe(first);
    expect(native.streamCalls.filter((c) => c.call === 'create')).toHaveLength(1);
  });

  it('delivers PCM to stream listeners until they are removed, and a reset clears them', () => {
    const audio = jest.requireMock<typeof import('expo-audio')>('expo-audio');
    const stream = new audio.AudioModule.AudioStream({
      sampleRate: 16000,
      channels: 1,
      encoding: 'int16',
    });
    const heard: number[][] = [];
    const sub = stream.addListener('audioStreamBuffer', (b) => {
      heard.push([...new Int16Array(b.data)]);
    });
    emitAudio(new Int16Array([1, -2, 3]));
    expect(heard).toEqual([[1, -2, 3]]);
    sub.remove();
    emitAudio(new Int16Array([9]));
    expect(heard).toHaveLength(1);
    stream.addListener('audioStreamBuffer', () => undefined);
    expect(native.audioListeners).toHaveLength(1);
    resetNativeMocks();
    expect(native.audioListeners).toHaveLength(0);
  });
});

import { act, screen } from '@testing-library/react-native';
import { Text } from 'react-native';
import { configureEngines, engines } from '../voice/engines';
import { requestComposer, useComposerRequest } from '../voice/typed/composerRequest';
import { useVoice } from '../voice/VoiceProvider';
import { FakeCommandModel, FakeMicPermission, FakeModelStore, FakeRecognizer } from './voice';
import { fakeVoice, installFakeEngines, renderWithVoice } from './voiceUi';

function Probe() {
  const voice = useVoice();
  return <Text testID="state">{voice.state.kind}</Text>;
}

describe('voiceUi', () => {
  it('fakeVoice records calls and set changes what useVoice returns', async () => {
    const voice = fakeVoice();
    await renderWithVoice(<Probe />, { voice });
    expect(screen.getByTestId('state')).toHaveTextContent('idle');

    voice.api.holdStart();
    voice.api.apply(['l1']);
    voice.api.submitTyped('move it');
    voice.api.retryWith({ asNote: true });
    expect(voice.calls).toEqual([
      'holdStart',
      'apply(["l1"])',
      'submitTyped("move it")',
      'retryWith({"asNote":true})',
    ]);

    await act(async () => voice.set({ kind: 'answer', content: { lead: 'Two', rows: [] } }));
    expect(screen.getByTestId('state')).toHaveTextContent('answer');
  });

  describe('installFakeEngines', () => {
    let installed: ReturnType<typeof installFakeEngines>;

    it('installFakeEngines reaches the provider and is reset after the test', async () => {
      installed = installFakeEngines();
      installed.models.set({ phase: 'skipped' });
      await renderWithVoice(<Probe />, { engines: installed });
      await act(async () => {});
      expect(engines().models).toBe(installed.models);
      expect(screen.getByTestId('state')).toHaveTextContent('disabled');
    });

    // Runs after the test above: its afterEach has put the not-ready engines back.
    it('starts the next test with engines that are not ready', () => {
      const e = engines();
      expect(e.models).not.toBe(installed.models);
      expect(e.recognizer.ready()).toBe(false);
      expect(e.model.ready()).toBe(false);
      expect(e.models.status().phase).toBe('none');
      return expect(e.mic.status()).resolves.toBe('undetermined');
    });
  });

  describe('resets between tests', () => {
    it('leaves a configureEngines call and an unconsumed composer request behind', () => {
      configureEngines({
        recognizer: new FakeRecognizer(),
        model: new FakeCommandModel(),
        models: new FakeModelStore(),
        mic: new FakeMicPermission(),
      });
      requestComposer({ mode: 'note', text: 'left over', reason: null, questionNumber: null });
    });

    it('starts the next test with neither', async () => {
      expect(engines().recognizer.ready()).toBe(false);
      function Pending() {
        return <Text testID="pending">{useComposerRequest().request?.text ?? 'none'}</Text>;
      }
      await renderWithVoice(<Pending />);
      expect(screen.getByTestId('pending')).toHaveTextContent('none');
    });
  });
});

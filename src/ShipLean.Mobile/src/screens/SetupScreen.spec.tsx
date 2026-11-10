import { act, screen } from '@testing-library/react-native';
import { router } from 'expo-router';
import { links } from '../links';
import { renderApp } from '../test-support/renderApp';
import { kvValues } from '../test-support/storage';
import { FakeModelStore } from '../test-support/voice';
import { installFakeEngines } from '../test-support/voiceUi';
import { SETUP_OFFERED_KEY } from '../setup/firstRun';
import { setupSubId } from './SetupScreen';
import { screenSetup } from './testIds';

describe('SetupScreen', () => {
  it.each([
    [true, '8 GB phone · Wi-Fi only'],
    [false, '8 GB phone · mobile data'],
  ])(
    'the Downloading page names the phone and the network (wifiOnly %s)',
    async (wifiOnly, sub) => {
      kvValues.set(SETUP_OFFERED_KEY, '1');
      const models = new FakeModelStore();
      models.set({ phase: 'downloading', wifiOnly });
      installFakeEngines({ models });
      await renderApp();
      await act(() => router.push(links.setup('downloading')));

      expect(await screen.findByTestId(screenSetup('downloading'))).toBeOnTheScreen();
      expect(screen.getByTestId(setupSubId)).toHaveTextContent(sub);
    },
  );
});

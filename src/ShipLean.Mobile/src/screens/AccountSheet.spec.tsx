import { fireEvent, screen, waitFor } from '@testing-library/react-native';
import { openBrowserAsync } from 'expo-web-browser';
import { apiError, fakeApi } from '../test-support/fakeApi';
import { signedInApi } from '../test-support/signedInApi';
import { TEST_ORIGIN } from '../test-support/builders';
import { renderApp } from '../test-support/renderApp';
import { installFakeEngines } from '../test-support/voiceUi';
import { projectsIds } from './projects/testIds';
import { accountIds } from './account/testIds';
import { screenIds } from './testIds';

test('shows name, email, host and version', async () => {
  await renderApp({ url: '/account' });

  expect(await screen.findByTestId(screenIds.account)).toBeOnTheScreen();
  await waitFor(() =>
    expect(screen.getByTestId(accountIds.name)).toHaveTextContent(/Joel Dickson/),
  );
  expect(screen.getByTestId(accountIds.email)).toHaveTextContent(/joel@shiplean.local/);
  expect(screen.getByTestId(accountIds.host)).toHaveTextContent(/^shiplean.test/);
  await waitFor(() =>
    expect(screen.getByTestId(accountIds.version)).toHaveTextContent(/1\.4\.0\+7f26ef0a/),
  );
});

test('Profile row opens the server profile', async () => {
  await renderApp({ url: '/account' });

  await fireEvent.press(await screen.findByTestId(accountIds.profile));

  expect(openBrowserAsync).toHaveBeenCalledWith(`${TEST_ORIGIN}/profile`);
});

test('the avatar on Projects opens the sheet', async () => {
  await renderApp();

  await fireEvent.press(await screen.findByTestId(projectsIds.avatar));

  expect(await screen.findByTestId(screenIds.account)).toBeOnTheScreen();
});

test('shows Unavailable when me and health fail', async () => {
  await renderApp({
    url: '/account',
    api: fakeApi({
      ...signedInApi(),
      'GET /api/me': apiError(500, 'internal_error'),
      'GET /api/health': apiError(500, 'internal_error'),
    }),
  });

  await waitFor(
    () => expect(screen.getByTestId(accountIds.name)).toHaveTextContent(/Unavailable/),
    {
      timeout: 5000, // react-query retries a failed query once after a 1 s delay.
    },
  );
  expect(screen.getByTestId(accountIds.version)).toHaveTextContent(/Unavailable/);
  expect(screen.getByTestId(accountIds.email)).toHaveTextContent(/joel@shiplean.local/);
});

describe('AccountSheet › Voice row', () => {
  it('the Voice row shows the state and opens Settings › Voice', async () => {
    const { mic } = installFakeEngines();
    mic.set('granted');
    await renderApp({ url: '/account' });

    await waitFor(() =>
      expect(screen.getByTestId(accountIds.voiceValue)).toHaveTextContent('Ready · Qwen3 1.7B'),
    );
    await fireEvent.press(screen.getByTestId(accountIds.voice));

    expect(await screen.findByTestId(screenIds.settingsVoice)).toBeOnTheScreen();
  });

  it('the Voice row says mic off when the microphone is refused', async () => {
    const { mic } = installFakeEngines();
    mic.set('deniedFinal');
    await renderApp({ url: '/account' });

    await waitFor(() =>
      expect(screen.getByTestId(accountIds.voiceValue)).toHaveTextContent('Ready · mic off'),
    );
  });
});

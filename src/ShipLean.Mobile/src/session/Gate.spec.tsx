import { getMe } from '@shiplean/core/api/me';
import { act, screen } from '@testing-library/react-native';
import { screenIds } from '../screens/testIds';
import { TEST_ORIGIN, TEST_SECRET } from '../test-support/builders';
import { fakeApi, sessionEnded } from '../test-support/fakeApi';
import { renderApp } from '../test-support/renderApp';
import { signedInApi } from '../test-support/signedInApi';
import { secureValues } from '../test-support/storage';

test('gate_WhenSignedOut_ShouldShowSignIn', async () => {
  await renderApp({ signedIn: false });

  expect(await screen.findByTestId(screenIds.signIn)).toBeOnTheScreen();
  expect(screen.queryByTestId(screenIds.projects)).toBeNull();
});

test('gate_WhenSignedIn_ShouldShowProjects', async () => {
  await renderApp({ api: fakeApi(signedInApi()) });

  expect(await screen.findByTestId(screenIds.projects)).toBeOnTheScreen();
  expect(screen.queryByTestId(screenIds.signIn)).toBeNull();
});

test('gate_WhenARequestAnswersSessionEnded_ShouldReturnToSignInWithTheServerKept', async () => {
  const api = fakeApi({ ...signedInApi(), 'GET /api/me': sessionEnded() });
  await renderApp({ api });
  await screen.findByTestId(screenIds.projects);

  await act(async () => {
    await getMe().catch(() => undefined);
  });

  expect(await screen.findByTestId(screenIds.signIn)).toBeOnTheScreen();
  expect(screen.getByTestId(screenIds.signInServerField)).toHaveDisplayValue(TEST_ORIGIN);
  expect(api.callsTo('GET /api/me')).toEqual([
    expect.objectContaining({
      origin: TEST_ORIGIN,
      headers: expect.objectContaining({ Authorization: `Bearer ${TEST_SECRET}` }),
    }),
  ]);
  expect([...secureValues.values()]).not.toContain(TEST_SECRET);
});

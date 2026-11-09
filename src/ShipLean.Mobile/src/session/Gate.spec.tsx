import { getMe, meKey } from '@shiplean/core/api/me';
import { act, screen } from '@testing-library/react-native';
import { queryClient } from '../queryClient';
import { screenIds } from '../screens/testIds';
import { TEST_ORIGIN, TEST_SECRET, aMe } from '../test-support/builders';
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
  await renderApp({ api: fakeApi(signedInApi()) });
  await screen.findByTestId(screenIds.projects);
  // Projects reads me itself, so the request that ends the session is made once it has settled.
  await screen.findByText('JD');
  const api = fakeApi({ ...signedInApi(), 'GET /api/me': sessionEnded() });
  queryClient.setQueryData(meKey, aMe());

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
  expect(queryClient.getQueryCache().getAll()).toEqual([]);
  expect([...secureValues.values()].join('\n')).not.toContain(TEST_SECRET);
});

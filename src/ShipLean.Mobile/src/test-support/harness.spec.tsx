import { exchangeSignInCode } from '@shiplean/core/api/deviceSessions';
import { getMe } from '@shiplean/core/api/me';
import { screen } from '@testing-library/react-native';
import { Screen } from '../ui/Screen';
import { aDeviceSignIn } from './builders';
import { fakeApi } from './fakeApi';
import { renderScreen } from './renderScreen';

test('fakeApi_WhenARouteIsUnlisted_ShouldAnswer404NotFound', async () => {
  fakeApi({ 'GET /api/projects': [] });

  await expect(getMe()).rejects.toMatchObject({ status: 404, code: 'not_found' });
});

test('callsTo_WhenRequestsAreMade_ShouldRecordMethodPathAndBody', async () => {
  const api = fakeApi({
    'POST /api/auth/device-sessions/exchange': { status: 201, body: aDeviceSignIn() },
  });

  const signedIn = await exchangeSignInCode({ code: 'c', verifier: 'v' });

  expect(signedIn.me.email).toBe(aDeviceSignIn().me.email);
  expect(api.callsTo('POST /api/auth/device-sessions/exchange')).toEqual([
    expect.objectContaining({
      method: 'POST',
      path: '/api/auth/device-sessions/exchange',
      body: { code: 'c', verifier: 'v' },
    }),
  ]);
  expect(api.callsTo('GET /api/me')).toEqual([]);
});

test('fakeApi_WhenAnEntryIsAFunction_ShouldAnswerWithItsResult', async () => {
  fakeApi({
    'GET /api/me': (request) => ({
      status: 401,
      body: { error: request.path, code: 'unauthorized' },
    }),
  });

  await expect(getMe()).rejects.toMatchObject({ status: 401, message: '/api/me' });
});

test('renderScreen_WhenRendered_ShouldMountTheUiOnAPhone', async () => {
  await renderScreen(<Screen testID="harness-root">{null}</Screen>, { scheme: 'dark' });

  expect(screen.getByTestId('harness-root')).toBeOnTheScreen();
});

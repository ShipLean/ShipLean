import { appRedirect } from '@shiplean/core/handoff';
import { fireEvent, screen, waitFor } from '@testing-library/react-native';
import { openAuthSessionAsync, type WebBrowserAuthSessionResult } from 'expo-web-browser';
import { session } from '../session/session';
import { DEFAULT_ORIGIN } from '../session/sessionStore';
import { TEST_SECRET, aDeviceSignIn, aHealth, aMe } from '../test-support/builders';
import { apiError, fakeApi, type ApiTable } from '../test-support/fakeApi';
import { renderApp } from '../test-support/renderApp';
import { renderScreen } from '../test-support/renderScreen';
import { signedInApi } from '../test-support/signedInApi';
import { secureValues } from '../test-support/storage';
import { fieldErrorId, fieldHintId } from '../ui/testIds';
import { SignInScreen } from './SignInScreen';
import { signInIds } from './signIn/testIds';
import { screenIds } from './testIds';

const field = screenIds.signInServerField;
const EXCHANGE = 'POST /api/auth/device-sessions/exchange';

const stateOf = (url: string) => /[?&]state=([^&]+)/.exec(url)?.[1] ?? '';

// The browser comes back with what `redirect` builds from the state it was opened with.
function browserReturns(redirect: (state: string) => string | null) {
  jest.mocked(openAuthSessionAsync).mockImplementationOnce(async (url: string) => {
    const back = redirect(stateOf(url));
    return (
      back === null ? { type: 'cancel' } : { type: 'success', url: back }
    ) as WebBrowserAuthSessionResult;
  });
}

function api(overrides: ApiTable = {}) {
  return fakeApi({ ...signedInApi(), [EXCHANGE]: aDeviceSignIn(), ...overrides });
}

async function typeAddress(text: string, { leave = true } = {}) {
  await fireEvent.changeText(screen.getByTestId(field), text);
  if (leave) await fireEvent(screen.getByTestId(field), 'blur');
}

const signInButton = () => screen.getByTestId(signInIds.button);
const enabled = () => signInButton().props.accessibilityState?.disabled !== true;

test('first run prefills the default server and checks it', async () => {
  api();
  await renderScreen(<SignInScreen />);

  expect(screen.getByTestId(field)).toHaveDisplayValue(DEFAULT_ORIGIN);
  expect(await screen.findByTestId(fieldHintId(field))).toHaveTextContent(
    'ShipLean 1.4.0 · connected',
  );
  expect(enabled()).toBe(true);
  expect(screen.getByTestId(signInIds.version)).toBeOnTheScreen();
});

test('shows each normalisation message', async () => {
  api();
  await renderScreen(<SignInScreen />);

  for (const [input, message] of [
    ['', 'Enter the address of your ShipLean server.'],
    ['ftp://example.com', 'The address must start with http:// or https://.'],
    ['https://exa mple.com', "That doesn't look like a server address."],
    ['https://me:pw@example.com', 'Leave the user name and password out of the address.'],
  ] as const) {
    await typeAddress(input);
    expect(screen.getByTestId(fieldErrorId(field))).toHaveTextContent(message);
    expect(enabled()).toBe(false);
  }
});

test('each check state', async () => {
  const health = api({ 'GET /api/health': aHealth({ version: '2.0.1+abc' }) });
  await renderScreen(<SignInScreen />);

  await typeAddress('http://ok.test:5000');
  expect(await screen.findByTestId(fieldHintId(field))).toHaveTextContent(
    'ShipLean 2.0.1 · connected',
  );
  expect(health.callsTo('GET /api/health').at(-1)?.url).toBe('http://ok.test:5000/api/health');

  api({ 'GET /api/health': { hello: 'world' } });
  await typeAddress('not-shiplean.test');
  expect(await screen.findByTestId(fieldErrorId(field))).toHaveTextContent(
    "not-shiplean.test isn't a ShipLean server. Check the address.",
  );

  api({ 'GET /api/health': apiError(503, 'internal_error') });
  await typeAddress('down.test');
  expect(await screen.findByTestId(fieldErrorId(field))).toHaveTextContent(
    "Can't reach down.test. Check the address and that the server is running.",
  );
  expect(enabled()).toBe(false);
});

test('Sign in disabled until the check passes for the current text', async () => {
  const health = api();
  await renderScreen(<SignInScreen />);
  await screen.findByTestId(fieldHintId(field));
  expect(enabled()).toBe(true);

  await typeAddress('other.test', { leave: false });
  expect(enabled()).toBe(false);

  await fireEvent(screen.getByTestId(field), 'blur');
  await waitFor(() => expect(enabled()).toBe(true));
  expect(health.callsTo('GET /api/health').at(-1)?.origin).toBe('https://other.test');

  await typeAddress('another.test', { leave: false });
  expect(enabled()).toBe(false);
});

test('a superseded check is aborted and never decides the button', async () => {
  const server = api();
  const answering = globalThis.fetch;
  const signals: AbortSignal[] = [];
  // A real fetch rejects when its signal fires, and a check that never answers must not decide anything.
  globalThis.fetch = ((url: string, init?: RequestInit) => {
    if (!url.startsWith('https://slow.test')) return answering(url, init);
    signals.push(init?.signal as AbortSignal);
    return new Promise((_resolve, reject) => {
      init?.signal?.addEventListener('abort', () => reject(new DOMException('x', 'AbortError')));
    });
  }) as typeof fetch;
  await renderScreen(<SignInScreen />);

  await typeAddress('slow.test');
  await typeAddress('fast.test');

  expect(signals[0].aborted).toBe(true);
  await waitFor(() => expect(enabled()).toBe(true));
  expect(server.callsTo('GET /api/health').at(-1)?.origin).toBe('https://fast.test');
});

test('cancel stays on Sign in with no error', async () => {
  api();
  await renderScreen(<SignInScreen />);
  await waitFor(() => expect(enabled()).toBe(true));
  browserReturns(() => null);

  await fireEvent.press(signInButton());

  await waitFor(() => expect(openAuthSessionAsync).toHaveBeenCalled());
  await waitFor(() => expect(enabled()).toBe(true));
  expect(screen.queryByTestId(signInIds.error)).toBeNull();
  expect(session.get().signedIn).toBe(false);
});

test('error=unsupported', async () => {
  api();
  await renderScreen(<SignInScreen />);
  await waitFor(() => expect(enabled()).toBe(true));
  browserReturns((state) => appRedirect({ error: 'unsupported', state }));

  await fireEvent.press(signInButton());

  expect(await screen.findByTestId(signInIds.error)).toHaveTextContent(
    "This server doesn't support the app yet.",
  );
  expect(session.get()).toMatchObject({ signedIn: false, pending: null });
  expect([...secureValues.keys()]).toEqual([]);
});

test('state mismatch', async () => {
  const server = api();
  await renderScreen(<SignInScreen />);
  await waitFor(() => expect(enabled()).toBe(true));
  browserReturns(() => appRedirect({ code: 'the-code', state: 'not-ours' }));

  await fireEvent.press(signInButton());

  expect(await screen.findByTestId(signInIds.error)).toHaveTextContent(
    "Sign-in didn't finish. Try again.",
  );
  expect(server.callsTo(EXCHANGE)).toEqual([]);
  expect([...secureValues.keys()]).toEqual([]);
});

test('failed exchange shows the status', async () => {
  api({ [EXCHANGE]: apiError(400, 'validation') });
  await renderScreen(<SignInScreen />);
  await waitFor(() => expect(enabled()).toBe(true));
  browserReturns((state) => appRedirect({ code: 'the-code', state }));

  await fireEvent.press(signInButton());

  expect(await screen.findByTestId(signInIds.error)).toHaveTextContent(
    'Sign-in failed (400). Try again.',
  );
  expect(session.get()).toMatchObject({ signedIn: false, pending: null });
  expect([...secureValues.keys()]).toEqual([]);
});

// Last: it leaves the shared session store signed in.
test('browser success signs in', async () => {
  const server = api();
  await renderApp({ signedIn: false, api: server });
  await screen.findByTestId(screenIds.signIn);
  await waitFor(() => expect(enabled()).toBe(true));
  browserReturns((state) => appRedirect({ code: 'the-code', state }));

  await fireEvent.press(signInButton());

  expect(await screen.findByTestId(screenIds.projects)).toBeOnTheScreen();
  const secret = JSON.parse([...secureValues.values()].join('')) as { secret: string };
  expect(secret.secret).toBe(TEST_SECRET);
  expect(session.get().email).toBe(aMe().email);
});

import { act, fireEvent, screen, waitFor } from '@testing-library/react-native';
import { platform } from '@shiplean/core/platform';
import { scopeOf } from '../actions/scope';
import { loadDraft, saveDraft } from '../dock/drafts';
import { lostSession } from '../session/lostSession';
import { session } from '../session/session';
import { kvValues, secureValues } from '../test-support/storage';
import { fakeApi, sessionEnded } from '../test-support/fakeApi';
import { projectRowId } from './projects/testIds';
import { renderApp } from '../test-support/renderApp';
import { signedInApi } from '../test-support/signedInApi';
import { accountIds } from './account/testIds';
import { screenIds } from './testIds';

const END = 'DELETE /api/auth/device-sessions/current';

const confirmSignOut = async () => {
  await fireEvent.press(await screen.findByTestId(accountIds.signOut));
  await fireEvent.press(await screen.findByTestId(accountIds.confirmYes));
};

function expectNoSecret() {
  expect([...secureValues.values()].join('\n')).not.toContain('shl_dev_');
}

test('asks first; Cancel stays signed in', async () => {
  const { api } = await renderApp({
    url: '/account',
    api: fakeApi({ ...signedInApi(), [END]: { status: 204 } }),
  });

  await fireEvent.press(await screen.findByTestId(accountIds.signOut));
  expect(await screen.findByTestId(accountIds.confirm)).toBeOnTheScreen();
  expect(api.callsTo(END)).toHaveLength(0);
  await fireEvent.press(screen.getByTestId(accountIds.confirmCancel));

  expect(await screen.findByTestId(accountIds.signOut)).toBeOnTheScreen();
  expect(api.callsTo(END)).toHaveLength(0);
  expect(screen.queryByTestId(screenIds.signIn)).toBeNull();
  expect([...secureValues.values()].join('\n')).toContain('shl_dev_');
});

test('confirm signs out to screen-sign-in with sign-in-server-field still holding the server', async () => {
  const { api } = await renderApp({
    url: '/account',
    api: fakeApi({ ...signedInApi(), [END]: { status: 204 } }),
  });

  await confirmSignOut();

  expect(await screen.findByTestId(screenIds.signIn)).toBeOnTheScreen();
  expect(screen.getByTestId(screenIds.signInServerField)).toHaveDisplayValue(
    'https://shiplean.test',
  );
  expect(api.callsTo(END)).toHaveLength(1);
  expect(
    api.callsTo(END)[0].headers.Authorization ?? api.callsTo(END)[0].headers.authorization,
  ).toMatch(/^Bearer shl_dev_/);
  expectNoSecret();
});

test('offline sign-out still lands on Sign in', async () => {
  const { api } = await renderApp({
    url: '/account',
    api: fakeApi({
      ...signedInApi(),
      [END]: () => {
        throw new TypeError('Network request failed');
      },
    }),
  });

  await confirmSignOut();

  expect(await screen.findByTestId(screenIds.signIn)).toBeOnTheScreen();
  expect(screen.getByTestId(screenIds.signInServerField)).toHaveDisplayValue(
    'https://shiplean.test',
  );
  expect(api.callsTo(END)).toHaveLength(1);
  expectNoSecret();
});

test('a 401 session_ended on Projects goes to Sign in', async () => {
  await renderApp({ api: fakeApi({ ...signedInApi(), 'GET /api/projects': sessionEnded() }) });

  expect(await screen.findByTestId(screenIds.signIn)).toBeOnTheScreen();
  await waitFor(() => expectNoSecret());
  expect(screen.queryByTestId(projectRowId('SHIP'))).toBeNull();
});

test('an explicit sign-out clears the dock drafts, a lost session keeps them', async () => {
  const { api } = await renderApp({
    url: '/account',
    api: fakeApi({ ...signedInApi(), [END]: { status: 204 } }),
  });
  const scope = scopeOf(session.get())!;
  const store = platform().persistentStore();
  saveDraft(store, scope, 'item-1', { text: 'half a note', mode: 'note' });
  saveDraft(store, scope, 'item-2', { text: 'approve D10', mode: 'command' });

  await confirmSignOut();
  expect(await screen.findByTestId(screenIds.signIn)).toBeOnTheScreen();
  expect(api.callsTo(END)).toHaveLength(1);
  expect([...kvValues.keys()].filter((k) => k.startsWith('shiplean.dockDraft'))).toEqual([]);
});

test('a lost session keeps the dock drafts', async () => {
  await renderApp({ url: '/account', api: fakeApi(signedInApi()) });
  const scope = scopeOf(session.get())!;
  const store = platform().persistentStore();
  saveDraft(store, scope, 'item-1', { text: 'half a note', mode: 'note' });

  await act(() => lostSession());

  expect(loadDraft(store, scope, 'item-1')).toEqual({ text: 'half a note', mode: 'note' });
});

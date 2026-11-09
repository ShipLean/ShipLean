import { memoryStore } from '@shiplean/core/platform';
import { describe, expect, test } from 'vitest';
import { createConnectivity, createMobilePlatform } from './mobilePlatformCore';
import type { SessionState } from './session/sessionStore';

const signedOut: SessionState = {
  origin: 'https://a.test',
  email: 'a@a.test',
  secret: null,
  signedIn: false,
  pending: null,
};

function platformFor(state: SessionState, lostSession: () => void = () => undefined) {
  return createMobilePlatform({
    session: { get: () => state },
    kv: memoryStore(),
    connectivity: createConnectivity(),
    lostSession,
  });
}

describe('createMobilePlatform', () => {
  test('authHeaders_WhenSignedIn_ShouldSendTheBearerSecretToTheStoredServer', () => {
    const platform = platformFor({ ...signedOut, secret: 'shl_dev_a', signedIn: true });

    expect(platform.apiOrigin()).toBe('https://a.test');
    expect(platform.authHeaders()).toEqual({ Authorization: 'Bearer shl_dev_a' });
  });

  test('authHeaders_WhenSignedOut_ShouldSendNothing', () => {
    expect(platformFor(signedOut).authHeaders()).toEqual({});
  });

  test('authHeaders_WhenASignInIsPending_ShouldUseTheOverlaysServerAndSecret', () => {
    const pending = platformFor({
      ...signedOut,
      secret: 'shl_dev_a',
      pending: { origin: 'https://b.test', secret: 'shl_dev_b' },
    });

    expect(pending.apiOrigin()).toBe('https://b.test');
    expect(pending.authHeaders()).toEqual({ Authorization: 'Bearer shl_dev_b' });
  });

  test('authHeaders_WhenThePendingSignInHasNoSecretYet_ShouldSendNothing', () => {
    const pending = platformFor({
      ...signedOut,
      secret: 'shl_dev_a',
      pending: { origin: 'https://b.test', secret: null },
    });

    expect(pending.apiOrigin()).toBe('https://b.test');
    expect(pending.authHeaders()).toEqual({});
  });

  test('apiOrigin_WhenThereIsNoOrigin_ShouldBeEmpty', () => {
    expect(platformFor({ ...signedOut, origin: null }).apiOrigin()).toBe('');
  });

  test('credentials_WhenAsked_ShouldOmitCookies', () => {
    expect(platformFor(signedOut).credentials()).toBe('omit');
  });

  test('onUnauthorized_WhenARequestLosesTheSession_ShouldCallLostSession', () => {
    const reasons: string[] = [];
    const platform = platformFor(signedOut, () => reasons.push('lost'));

    platform.onUnauthorized('session_ended');

    expect(reasons).toEqual(['lost']);
  });
});

describe('createConnectivity', () => {
  test('isOffline_WhenNotToldYet_ShouldBeOnline', () => {
    expect(createConnectivity().isOffline()).toBe(false);
  });

  test('onOnline_WhenTheConnectionComesBack_ShouldNotifyOnce', () => {
    const connectivity = createConnectivity();
    let online = 0;
    connectivity.onOnline(() => online++);

    connectivity.update(true);
    connectivity.update(false);
    const offline = connectivity.isOffline();
    connectivity.update(null);
    connectivity.update(true);

    expect(offline).toBe(true);
    expect(connectivity.isOffline()).toBe(false);
    expect(online).toBe(1);
  });
});

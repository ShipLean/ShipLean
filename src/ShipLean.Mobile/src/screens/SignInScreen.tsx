import { exchangeSignInCode } from '@shiplean/core/api/deviceSessions';
import { getMe, meKey } from '@shiplean/core/api/me';
import Constants from 'expo-constants';
import { getRandomBytes } from 'expo-crypto';
import * as Device from 'expo-device';
import { openAuthSessionAsync } from 'expo-web-browser';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Image, Text, View } from 'react-native';
import logo from '../../assets/splash-icon.png';
import { queryClient } from '../queryClient';
import { session } from '../session/session';
import { hostOfOrigin, normaliseServerAddress } from '../session/serverAddress';
import { checkLine, checkServer, type ServerCheck } from '../session/serverCheck';
import { createSignIn } from '../session/signIn';
import { useSession } from '../session/useSession';
import { Button } from '../ui/Button';
import { Screen } from '../ui/Screen';
import { TextField } from '../ui/TextField';
import { TopBar } from '../ui/TopBar';
import { signInIds } from './signIn/testIds';
import { screenIds } from './testIds';

const MAX_DEVICE_NAME = 100;

function deviceName(): string {
  const name = (Device.deviceName ?? Device.modelName ?? '').trim() || 'Phone';
  return name.slice(0, MAX_DEVICE_NAME);
}

// Network calls go through the core, which reads the session's origin and secret: the pending overlay's, here.
const flow = createSignIn({
  session,
  openAuthSession: (url, redirect) => openAuthSessionAsync(url, redirect),
  exchange: exchangeSignInCode,
  getMe,
  seedMe: (me) => queryClient.setQueryData(meKey, me),
  deviceName,
  randomBytes: getRandomBytes,
});

// The check for one address: still running, or finished.
type CheckState = { origin: string; result: ServerCheck | null };

export function SignInScreen() {
  const { origin: remembered } = useSession();
  const [text, setText] = useState(remembered ?? '');
  const [check, setCheck] = useState<CheckState | null>(null);
  // Set once the person has left the field, so a half-typed address isn't scolded.
  const [touched, setTouched] = useState(false);
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<string | null>(null);
  const running = useRef<AbortController | null>(null);

  const address = useMemo(() => normaliseServerAddress(text), [text]);

  const runCheck = useCallback((origin: string) => {
    running.current?.abort();
    const controller = new AbortController();
    running.current = controller;
    setCheck({ origin, result: null });
    void checkServer(origin, { fetch: globalThis.fetch, signal: controller.signal }).then(
      (result) => {
        if (result.state === 'aborted' || running.current !== controller) return;
        setCheck({ origin, result });
      },
    );
  }, []);

  // The remembered server (or the default) is checked as the screen opens, so Sign in is ready without a tap.
  const opened = useRef(remembered);
  useEffect(() => {
    const first = normaliseServerAddress(opened.current ?? '');
    if (first.ok) runCheck(first.origin);
    return () => running.current?.abort();
  }, [runCheck]);

  function onChangeText(next: string) {
    running.current?.abort();
    running.current = null;
    setCheck(null);
    setFailure(null);
    setTouched(false);
    setText(next);
  }

  function onBlur() {
    setTouched(true);
    if (!address.ok) return;
    // Leaving the field checks the address unless it already passed or is being checked; a failed one is retried.
    const state = check?.origin === address.origin ? check.result?.state : undefined;
    if (state !== 'ok' && !(check?.origin === address.origin && check.result === null)) {
      runCheck(address.origin);
    }
  }

  const passed = address.ok && check?.origin === address.origin && check.result?.state === 'ok';

  let hint: string | undefined;
  let error: string | undefined;
  if (!address.ok) {
    if (touched) error = address.error;
  } else if (check?.origin === address.origin) {
    if (check.result === null) hint = 'Checking…';
    else {
      const line = checkLine(check.result, address.host);
      if (check.result.state === 'ok') hint = line ?? undefined;
      else error = line ?? undefined;
    }
  }

  async function onSignIn() {
    if (!address.ok || !passed) return;
    setBusy(true);
    setFailure(null);
    try {
      const outcome = await flow.signIn(address.origin);
      if (outcome.kind === 'failed') setFailure(outcome.message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <Screen testID={screenIds.signIn}>
      <TopBar
        title={remembered === null ? 'ShipLean' : `ShipLean · ${hostOfOrigin(remembered)}`}
        left={
          <Image
            testID={signInIds.logo}
            accessibilityLabel="ShipLean"
            source={logo}
            style={{ width: 24, height: 24 }}
          />
        }
      />
      <View>
        <TextField
          label="Server"
          value={text}
          onChangeText={onChangeText}
          onBlur={onBlur}
          hint={hint}
          error={error}
          testID={screenIds.signInServerField}
        />
        <Button
          label="Sign in"
          onPress={() => void onSignIn()}
          disabled={!passed}
          busy={busy}
          testID={signInIds.button}
        />
        {failure !== null ? (
          <Text testID={signInIds.error} accessibilityRole="alert">
            {failure}
          </Text>
        ) : null}
      </View>
      <Text testID={signInIds.version}>
        {`ShipLean ${Constants.expoConfig?.version ?? ''}`.trim()}
      </Text>
    </Screen>
  );
}

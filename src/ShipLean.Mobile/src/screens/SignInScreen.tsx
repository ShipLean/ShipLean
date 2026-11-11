import { exchangeSignInCode } from '@shiplean/core/api/deviceSessions';
import { getMe, meKey } from '@shiplean/core/api/me';
import Constants from 'expo-constants';
import { getRandomBytes } from 'expo-crypto';
import * as Device from 'expo-device';
import { openAuthSessionAsync } from 'expo-web-browser';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Text, View } from 'react-native';
import { queryClient } from '../queryClient';
import { session } from '../session/session';
import { normaliseServerAddress } from '../session/serverAddress';
import { checkLine, checkServer, type ServerCheck } from '../session/serverCheck';
import { createSignIn } from '../session/signIn';
import { useSession } from '../session/useSession';
import { Body, Heading, StepBody, StepFoot } from '../setup/SetupParts';
import { useTheme } from '../theme';
import { Button } from '../ui/Button';
import { Icon } from '../ui/Icon';
import { Screen } from '../ui/Screen';
import { TextField } from '../ui/TextField';
import { TopBar } from '../ui/TopBar';
import { signInIds } from './signIn/testIds';
import { screenIds } from './testIds';
import { fieldHintId } from '../ui/testIds';

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
  const theme = useTheme();
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
      if (check.result.state !== 'ok') error = line ?? undefined;
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

  const connected = address.ok && passed && check?.result?.state === 'ok' ? check.result : null;
  const appVersion = Constants.expoConfig?.version ?? '';

  return (
    <Screen testID={screenIds.signIn}>
      <TopBar title="Sign in" sub="Step 1 of 3" />
      <StepBody testID={signInIds.body}>
        <View style={{ gap: theme.space[1] }}>
          <Heading>Sign in to your server</Heading>
          <Body muted>Use the address you open ShipLean at on the web.</Body>
        </View>
        <View>
          <TextField
            label="Server"
            value={text}
            onChangeText={onChangeText}
            onBlur={onBlur}
            onClear={() => onChangeText('')}
            hint={hint}
            error={error}
            testID={screenIds.signInServerField}
          />
          {connected === null ? null : (
            <View
              style={{
                flexDirection: 'row',
                alignItems: 'center',
                gap: theme.space[2],
                marginTop: theme.space[2],
              }}
            >
              <View
                testID={signInIds.connectedDot}
                style={{
                  width: 8,
                  height: 8,
                  borderRadius: 4,
                  backgroundColor: theme.colors.ok,
                }}
              />
              <Text
                testID={fieldHintId(screenIds.signInServerField)}
                style={{ color: theme.colors.muted, fontSize: theme.font.size.sm }}
              >
                <Text style={{ color: theme.colors.text, fontWeight: theme.font.weight.semibold }}>
                  {`ShipLean ${connected.version.split('+')[0]}`}
                </Text>
                {' · connected'}
              </Text>
            </View>
          )}
        </View>
        <View
          testID={signInIds.howCard}
          style={{
            flexDirection: 'row',
            gap: theme.space[3],
            padding: 14,
            borderRadius: 14,
            backgroundColor: theme.colors.surface2,
          }}
        >
          <Icon name="link" color={theme.colors.accent} />
          <View style={{ flex: 1, gap: 2 }}>
            <Text
              style={{
                color: theme.colors.text,
                fontSize: theme.font.size.body15,
                fontWeight: theme.font.weight.semibold,
              }}
            >
              Sign in opens your browser
            </Text>
            <Text style={{ color: theme.colors.muted, fontSize: theme.font.size.md }}>
              Password, Google and password changes work just as they do on the web. The app never
              asks for your password.
            </Text>
          </View>
        </View>
        {failure !== null ? (
          <Text
            testID={signInIds.error}
            accessibilityRole="alert"
            style={{ color: theme.colors.danger, fontSize: theme.font.size.md }}
          >
            {failure}
          </Text>
        ) : null}
      </StepBody>
      <StepFoot>
        <Button
          label="Sign in"
          onPress={() => void onSignIn()}
          disabled={!passed}
          busy={busy}
          testID={signInIds.button}
        />
        <Text
          testID={signInIds.version}
          style={{
            textAlign: 'center',
            color: theme.colors.muted,
            fontSize: theme.font.size.xs,
          }}
        >
          {`App version ${appVersion}`.trim()}
        </Text>
      </StepFoot>
    </Screen>
  );
}

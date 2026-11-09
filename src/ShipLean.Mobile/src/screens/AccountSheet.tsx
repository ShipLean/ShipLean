import { getHealth } from '@shiplean/core/api/health';
import { getMe, meKey } from '@shiplean/core/api/me';
import { useQuery } from '@tanstack/react-query';
import { openBrowserAsync } from 'expo-web-browser';
import { useState } from 'react';
import { Text, View } from 'react-native';
import { useSession } from '../session/useSession';
import { Button } from '../ui/Button';
import { Row } from '../ui/Row';
import { Sheet } from '../ui/Sheet';
import { accountIds } from './account/testIds';
import { signOutNow } from './account/signOutNow';
import { screenIds } from './testIds';

const hostOf = (origin: string | null) =>
  origin === null ? '' : origin.replace(/^[a-z]+:\/\//i, '');

export function AccountSheet() {
  const { origin, email } = useSession();
  const me = useQuery({ queryKey: meKey, queryFn: getMe });
  const health = useQuery({ queryKey: ['health'], queryFn: () => getHealth() });
  const [confirming, setConfirming] = useState(false);
  const [busy, setBusy] = useState(false);
  const host = hostOf(origin);

  if (confirming) {
    return (
      <Sheet title={`Sign out of ${host}?`} testID={screenIds.account}>
        <View testID={accountIds.confirm}>
          <Text testID={accountIds.confirmBody}>
            The server and your email stay filled in, so signing back in takes one tap.
          </Text>
          <Button
            label="Sign out"
            variant="danger"
            busy={busy}
            testID={accountIds.confirmYes}
            onPress={() => {
              setBusy(true);
              // The gate takes this sheet away once the secret is cleared.
              void signOutNow();
            }}
          />
          <Button
            label="Cancel"
            variant="ghost"
            disabled={busy}
            testID={accountIds.confirmCancel}
            onPress={() => setConfirming(false)}
          />
        </View>
      </Sheet>
    );
  }

  return (
    <Sheet title="Account" testID={screenIds.account}>
      <Row title={me.data?.name ?? ''} subtitle="Name" testID={accountIds.name} />
      <Row title={me.data?.email ?? email} subtitle="Email" testID={accountIds.email} />
      <Row title={host} subtitle="Server" testID={accountIds.host} />
      <Row
        title={health.data?.version ?? ''}
        subtitle="Server version"
        testID={accountIds.version}
      />
      <Row
        title="Profile, password and devices"
        subtitle="On the web"
        testID={accountIds.profile}
        onPress={() => {
          if (origin !== null) void openBrowserAsync(`${origin}/profile`);
        }}
      />
      <Button
        label="Sign out"
        variant="danger"
        testID={accountIds.signOut}
        onPress={() => setConfirming(true)}
      />
    </Sheet>
  );
}

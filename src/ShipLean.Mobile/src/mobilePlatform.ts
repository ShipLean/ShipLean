// The phone's Platform for @shiplean/core: the kv store, NetInfo, the session's server and secret, and a lost session
// sending the app back to Sign in. app/_layout.tsx configures it before anything renders.
import NetInfo from '@react-native-community/netinfo';
import { focusManager, onlineManager } from '@tanstack/react-query';
import { AppState } from 'react-native';
import { createConnectivity, createMobilePlatform } from './mobilePlatformCore';
import { lostSession } from './session/lostSession';
import { kvStore, session } from './session/session';

const connectivity = createConnectivity();
NetInfo.addEventListener((state) => connectivity.update(state.isConnected));

export const mobilePlatform = createMobilePlatform({
  session,
  kv: kvStore,
  connectivity,
  lostSession,
});

// react-query pauses while offline and resumes on reconnect; focus follows the app coming to the foreground.
export function connectReactQuery(): void {
  onlineManager.setEventListener((setOnline) =>
    NetInfo.addEventListener((state) => setOnline(state.isConnected !== false)),
  );
  focusManager.setEventListener((setFocused) => {
    const subscription = AppState.addEventListener('change', (status) =>
      setFocused(status === 'active'),
    );
    return () => subscription.remove();
  });
}

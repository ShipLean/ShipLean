import { configurePlatform } from '@shiplean/core/platform';
import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { SafeAreaProvider, initialWindowMetrics } from 'react-native-safe-area-context';
import { connectReactQuery, mobilePlatform } from '../src/mobilePlatform';
import { useSession } from '../src/session/useSession';
import { AppProviders } from '../src/shell/AppProviders';
import { useTheme } from '../src/theme/useTheme';

// Before anything renders or fetches. index.ts has already loaded the crypto polyfill the core reads.
configurePlatform(mobilePlatform);
connectReactQuery();

// Opening /account directly still has Projects under it.
export const unstable_settings = { anchor: 'index' };

// The gate: the session decides which screens exist. Losing the session (sign-out, or any request answering 401)
// clears the secret, the signed-in screens drop out of the stack and Sign in shows with the server and email kept.
export default function RootLayout() {
  const { signedIn } = useSession();
  const theme = useTheme();
  return (
    <SafeAreaProvider initialMetrics={initialWindowMetrics}>
      <AppProviders>
        <StatusBar style={theme.scheme === 'dark' ? 'light' : 'dark'} />
        <Stack screenOptions={{ headerShown: false }}>
          <Stack.Protected guard={signedIn}>
            <Stack.Screen name="index" />
            <Stack.Screen name="account" options={{ presentation: 'formSheet' }} />
            <Stack.Screen name="p/[key]" />
            <Stack.Screen name="settings/voice" />
            <Stack.Screen name="settings/voice/licences" />
            <Stack.Screen name="setup/[step]" />
            <Stack.Screen name="mic-tuning" />
          </Stack.Protected>
          <Stack.Protected guard={!signedIn}>
            <Stack.Screen name="sign-in" />
          </Stack.Protected>
        </Stack>
      </AppProviders>
    </SafeAreaProvider>
  );
}

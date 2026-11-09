import { configurePlatform } from '@shiplean/core/platform';
import { QueryClientProvider } from '@tanstack/react-query';
import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { SafeAreaProvider, initialWindowMetrics } from 'react-native-safe-area-context';
import { connectReactQuery, mobilePlatform } from '../src/mobilePlatform';
import { queryClient } from '../src/queryClient';
import { useSession } from '../src/session/useSession';
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
      <QueryClientProvider client={queryClient}>
        <StatusBar style={theme.scheme === 'dark' ? 'light' : 'dark'} />
        <Stack screenOptions={{ headerShown: false }}>
          <Stack.Protected guard={signedIn}>
            <Stack.Screen name="index" />
            <Stack.Screen name="account" options={{ presentation: 'formSheet' }} />
          </Stack.Protected>
          <Stack.Protected guard={!signedIn}>
            <Stack.Screen name="sign-in" />
          </Stack.Protected>
        </Stack>
      </QueryClientProvider>
    </SafeAreaProvider>
  );
}

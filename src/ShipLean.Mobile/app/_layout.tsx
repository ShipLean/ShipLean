import { Stack } from 'expo-router';

// TODO: the session gate, providers and status bar replace this bare stack.
export default function RootLayout() {
  return <Stack screenOptions={{ headerShown: false }} />;
}

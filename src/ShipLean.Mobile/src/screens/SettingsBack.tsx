import { useRouter } from 'expo-router';
import { Text } from 'react-native';
import { useTheme } from '../theme';

// The Back link on the left of a Settings page's top bar.
export function SettingsBack() {
  const router = useRouter();
  const theme = useTheme();
  if (!router.canGoBack()) return null;
  return (
    <Text
      accessibilityRole="button"
      onPress={() => router.back()}
      style={{ color: theme.colors.accent, fontSize: theme.font.size.md }}
    >
      Back
    </Text>
  );
}

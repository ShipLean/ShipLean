import { useRouter } from 'expo-router';
import { Text } from 'react-native';
import { Screen } from '../ui/Screen';
import { TopBar } from '../ui/TopBar';
import { useTheme } from '../theme/useTheme';

export interface StackPlaceholderProps {
  title: string;
  // The screen's root id from src/screens/testIds.ts.
  testID: string;
}

// A stack screen that isn't built yet: the top bar with a back button, and nothing else.
export function StackPlaceholder({ title, testID }: StackPlaceholderProps) {
  const router = useRouter();
  const theme = useTheme();
  const back = router.canGoBack() ? (
    <Text
      accessibilityRole="button"
      onPress={() => router.back()}
      style={{ color: theme.colors.accent, fontSize: theme.font.size.md }}
    >
      Back
    </Text>
  ) : undefined;
  return (
    <Screen testID={testID}>
      <TopBar title={title} left={back} />
    </Screen>
  );
}

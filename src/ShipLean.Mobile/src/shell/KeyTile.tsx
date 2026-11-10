import { useRouter } from 'expo-router';
import { Pressable, Text } from 'react-native';
import { links } from '../links';
import { tabIds } from '../screens/testIds';
import { useTheme } from '../theme/useTheme';

// The project's key tile at the top left of a tab root: back to Projects, whatever is under the tabs.
export function KeyTile({ projectKey }: { projectKey: string }) {
  const router = useRouter();
  const theme = useTheme();
  return (
    <Pressable
      testID={tabIds.rootTile}
      accessibilityRole="button"
      accessibilityLabel="Projects"
      hitSlop={theme.space[2]}
      onPress={() => router.dismissTo(links.projects())}
      style={{
        minWidth: theme.touch.control,
        minHeight: theme.touch.control,
        alignItems: 'center',
        justifyContent: 'center',
        borderRadius: theme.radius.md,
        backgroundColor: theme.colors.accentSoft,
      }}
    >
      <Text style={{ color: theme.colors.accent, fontWeight: theme.font.weight.bold }}>
        {projectKey}
      </Text>
    </Pressable>
  );
}

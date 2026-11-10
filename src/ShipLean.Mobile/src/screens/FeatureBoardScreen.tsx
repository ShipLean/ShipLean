import { useRouter } from 'expo-router';
import { Text } from 'react-native';
import { FeatureBoardView } from '../features/board/FeatureBoardView';
import { featureBoardIds } from '../features/testIds';
import { links } from '../links';
import { useTheme } from '../theme';
import { Screen } from '../ui/Screen';
import { TopBar } from '../ui/TopBar';
import { screenIds } from './testIds';

export interface FeatureBoardScreenProps {
  projectKey: string;
  featureKey: string;
}

// One Feature's board, read-only: its summary, then its stories and bugs grouped by state.
export function FeatureBoardScreen({ projectKey, featureKey }: FeatureBoardScreenProps) {
  const router = useRouter();
  const theme = useTheme();
  return (
    <Screen testID={screenIds.featureBoard}>
      <TopBar
        title={`${featureKey} board`}
        left={
          <Text
            testID={featureBoardIds.back}
            accessibilityRole="button"
            onPress={() => router.dismissTo(links.features(projectKey))}
            style={{ color: theme.colors.accent, fontSize: theme.font.size.md }}
          >
            Back
          </Text>
        }
      />
      <FeatureBoardView
        key={`${projectKey}/${featureKey}`}
        projectKey={projectKey}
        featureKey={featureKey}
      />
    </Screen>
  );
}

import { useLocalSearchParams } from 'expo-router';
import { isBacklogLevel } from '../../../../src/links';
import { BacklogScreen } from '../../../../src/screens/BacklogScreen';

export default function BacklogRoute() {
  const { key, level, epic, feature } = useLocalSearchParams<{
    key: string;
    level?: string;
    epic?: string;
    feature?: string;
  }>();
  return (
    <BacklogScreen
      projectKey={key}
      level={isBacklogLevel(level) ? level : undefined}
      epic={epic}
      feature={feature}
    />
  );
}

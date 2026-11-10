import { useLocalSearchParams } from 'expo-router';
import { FeatureBoardScreen } from '../../../../../src/screens/FeatureBoardScreen';

export default function FeatureBoardRoute() {
  const { key, featureKey } = useLocalSearchParams<{ key: string; featureKey: string }>();
  return <FeatureBoardScreen projectKey={key} featureKey={featureKey} />;
}

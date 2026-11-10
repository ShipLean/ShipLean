import { useLocalSearchParams } from 'expo-router';
import { FeaturesScreen } from '../../../../src/screens/FeaturesScreen';

export default function FeaturesRoute() {
  const { key } = useLocalSearchParams<{ key: string }>();
  return <FeaturesScreen projectKey={key} />;
}

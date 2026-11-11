import { useLocalSearchParams } from 'expo-router';
import { RetroScreen } from '../../../../src/screens/RetroScreen';

export default function RetroRoute() {
  const { key } = useLocalSearchParams<{ key: string }>();
  return <RetroScreen projectKey={key} />;
}

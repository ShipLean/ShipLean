import { useLocalSearchParams } from 'expo-router';
import { DigestScreen } from '../../../../src/screens/DigestScreen';

export default function DigestRoute() {
  const { key } = useLocalSearchParams<{ key: string }>();
  return <DigestScreen projectKey={key} />;
}

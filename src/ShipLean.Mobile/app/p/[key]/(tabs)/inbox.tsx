import { useLocalSearchParams } from 'expo-router';
import { InboxScreen } from '../../../../src/screens/InboxScreen';

export default function InboxRoute() {
  const { key, from } = useLocalSearchParams<{ key: string; from?: string }>();
  return <InboxScreen projectKey={key} from={from === 'setup' ? 'setup' : undefined} />;
}

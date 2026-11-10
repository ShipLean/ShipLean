import { useLocalSearchParams } from 'expo-router';
import { WorkItemScreen } from '../../../../src/screens/WorkItemScreen';

export default function WorkItemRoute() {
  const { key, itemKey } = useLocalSearchParams<{ key: string; itemKey: string }>();
  return <WorkItemScreen projectKey={key} itemKey={itemKey} />;
}

import { View } from 'react-native';
import { retroIds } from '../testIds';

export interface RetroFeaturesViewProps {
  projectId: string;
}

// TODO: the Features view replaces this stub.
export function RetroFeaturesView(props: RetroFeaturesViewProps) {
  void props;
  return <View testID={retroIds.features} />;
}

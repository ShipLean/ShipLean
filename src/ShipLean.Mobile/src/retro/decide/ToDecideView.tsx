import { View } from 'react-native';
import { retroIds } from '../testIds';

export interface ToDecideViewProps {
  projectId: string;
  sprintId: string;
}

// TODO: the To decide view replaces this stub.
export function ToDecideView(props: ToDecideViewProps) {
  void props;
  return <View testID={retroIds.toDecide} />;
}

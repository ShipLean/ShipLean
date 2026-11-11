import { View } from 'react-native';
import { adjustByHandId } from './AdjustByHand.testIds';

// TODO: Automatic on or off, and the manual slider for the speech line.
export function AdjustByHand() {
  return <View testID={adjustByHandId} />;
}

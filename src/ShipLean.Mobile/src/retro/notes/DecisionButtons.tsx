import { View } from 'react-native';
import { useTheme } from '../../theme';
import { Button } from '../../ui/Button';
import { retroAddActionId, retroNotNeededId } from '../testIds';

export interface DecisionButtonsProps {
  pointId: string;
  onAddAction: () => void;
  onNotNeeded: () => void;
}

// retro.html .foot: "+ Action item" as a soft button with the plus icon and "Not needed" as a link. The Notes card and
// the To decide row show the same pair.
export function DecisionButtons({ pointId, onAddAction, onNotNeeded }: DecisionButtonsProps) {
  const theme = useTheme();
  return (
    <View style={{ flexDirection: 'row', alignItems: 'center', gap: theme.space[2] }}>
      <Button
        label="Action item"
        icon="plus"
        variant="soft"
        size="sm"
        onPress={onAddAction}
        testID={retroAddActionId(pointId)}
      />
      <Button
        label="Not needed"
        variant="link"
        size="sm"
        onPress={onNotNeeded}
        testID={retroNotNeededId(pointId)}
      />
    </View>
  );
}

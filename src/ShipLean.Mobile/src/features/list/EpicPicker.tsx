import type { ParentRefDto } from '@shiplean/core/api/workItems';
import { Pressable, ScrollView, Text } from 'react-native';
import { useTheme } from '../../theme';
import { BottomSheet } from '../../ui/BottomSheet';
import { featureEpicOptionId, featuresIds } from '../testIds';

export interface EpicPickerProps {
  epics: ParentRefDto[];
  selectedId: string | null;
  open: boolean;
  onClose: () => void;
  // null clears the Epic.
  onPick: (epic: ParentRefDto | null) => void;
}

// The Epic the list is narrowed to, from the Epics the Features sit under.
export function EpicPicker({ epics, selectedId, open, onClose, onPick }: EpicPickerProps) {
  const theme = useTheme();
  const { colors } = theme;
  const option = (testID: string, label: string, selected: boolean, onPress: () => void) => (
    <Pressable
      key={testID}
      testID={testID}
      accessibilityRole="button"
      accessibilityState={{ selected }}
      onPress={onPress}
      style={{
        minHeight: theme.touch.row,
        justifyContent: 'center',
        borderBottomWidth: 1,
        borderBottomColor: colors.border,
      }}
    >
      <Text
        numberOfLines={2}
        style={{
          color: colors.text,
          fontSize: theme.font.size.md,
          fontWeight: selected ? theme.font.weight.bold : theme.font.weight.regular,
        }}
      >
        {label}
      </Text>
    </Pressable>
  );
  return (
    <BottomSheet title="Epic" open={open} onClose={onClose} testID={featuresIds.picker}>
      <ScrollView style={{ maxHeight: 420 }}>
        {option(featuresIds.pickerAll, 'All epics', selectedId === null, () => onPick(null))}
        {epics.map((epic) =>
          option(
            featureEpicOptionId(epic.key),
            `${epic.key} ${epic.title}`,
            epic.id === selectedId,
            () => onPick(epic),
          ),
        )}
      </ScrollView>
    </BottomSheet>
  );
}

import type { BacklogRowDto } from '@shiplean/core/api/views';
import { Pressable, ScrollView, Text } from 'react-native';
import { useTheme } from '../../theme';
import { BottomSheet } from '../../ui/BottomSheet';
import { StatePill } from '../../ui/StatePill';
import { backlogIds, backlogPickerOptionId } from '../testIds';

export interface ParentPickerProps {
  // 'Epic' at the Features level, 'Feature' at Stories.
  noun: 'Epic' | 'Feature';
  rows: BacklogRowDto[];
  selectedKey: string | null;
  open: boolean;
  onClose: () => void;
  // null clears the filter.
  onPick: (row: BacklogRowDto | null) => void;
}

// The Epic or Feature the list is narrowed to: all of them, or one.
export function ParentPicker({
  noun,
  rows,
  selectedKey,
  open,
  onClose,
  onPick,
}: ParentPickerProps) {
  const theme = useTheme();
  const { colors } = theme;
  const option = (
    testID: string,
    label: string,
    selected: boolean,
    onPress: () => void,
    state?: BacklogRowDto['state'],
  ) => (
    <Pressable
      key={testID}
      testID={testID}
      accessibilityRole="button"
      accessibilityState={{ selected }}
      onPress={onPress}
      style={{
        minHeight: theme.touch.row,
        flexDirection: 'row',
        alignItems: 'center',
        gap: theme.space[2],
        borderBottomWidth: 1,
        borderBottomColor: colors.border,
      }}
    >
      <Text
        numberOfLines={2}
        style={{
          flex: 1,
          color: colors.text,
          fontSize: theme.font.size.md,
          fontWeight: selected ? theme.font.weight.bold : theme.font.weight.regular,
        }}
      >
        {label}
      </Text>
      {state ? <StatePill state={state} /> : null}
    </Pressable>
  );
  return (
    <BottomSheet title={noun} open={open} onClose={onClose} testID={backlogIds.picker}>
      <ScrollView style={{ maxHeight: 420 }}>
        {option(backlogIds.pickerAll, `All ${noun.toLowerCase()}s`, selectedKey === null, () =>
          onPick(null),
        )}
        {rows.map((row) =>
          option(
            backlogPickerOptionId(row.key),
            `${row.key} ${row.title}`,
            row.key === selectedKey,
            () => onPick(row),
            row.state,
          ),
        )}
      </ScrollView>
    </BottomSheet>
  );
}

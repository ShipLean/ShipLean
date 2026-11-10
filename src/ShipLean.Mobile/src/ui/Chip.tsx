import { Pressable, Text } from 'react-native';
import { useTheme } from '../theme';
import { layout } from './layout';
import { countId } from './testIds';

export interface ChipProps {
  label: string;
  count?: number;
  selected: boolean;
  onPress: () => void;
  testID: string;
}

// A filter chip under the top bar (app.css .chip). Draws 32 high and reaches 44 with hitSlop.
export function Chip({ label, count, selected, onPress, testID }: ChipProps) {
  const theme = useTheme();
  const { colors } = theme;
  return (
    <Pressable
      testID={testID}
      accessibilityRole="button"
      accessibilityState={{ selected }}
      hitSlop={{ top: layout.compactSlop, bottom: layout.compactSlop }}
      onPress={onPress}
      style={{
        height: layout.compact,
        flexDirection: 'row',
        alignItems: 'center',
        gap: layout.chipGap,
        paddingHorizontal: theme.space[3],
        borderRadius: layout.compact / 2,
        borderWidth: 1,
        borderColor: selected ? 'transparent' : colors.border,
        backgroundColor: selected ? colors.accentSoft : colors.surface,
      }}
    >
      <Text
        style={{
          color: colors.text,
          fontSize: theme.font.size.base,
          fontWeight: selected ? theme.font.weight.medium : theme.font.weight.regular,
        }}
      >
        {label}
      </Text>
      {count === undefined ? null : (
        <Text
          testID={countId(testID)}
          style={{ color: colors.muted, fontSize: theme.font.size.sm }}
        >
          {count}
        </Text>
      )}
    </Pressable>
  );
}

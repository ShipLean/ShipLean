import { Pressable, Text, View } from 'react-native';
import { useTheme } from '../theme';
import { Icon, type IconName } from './Icon';
import { layout, tint } from './layout';
import { countId, dotId } from './testIds';

export interface ChipProps {
  label: string;
  count?: number;
  selected: boolean;
  onPress: () => void;
  testID: string;
  // Leading icon, a leading colour dot, a trailing chevron (a menu chip), and the tan fill of an ask chip.
  icon?: IconName;
  dot?: string;
  trailing?: 'chevron';
  tone?: 'ask';
}

// A filter chip under the top bar (app.css .chip). Draws 32 high and reaches 44 with hitSlop.
export function Chip({
  label,
  count,
  selected,
  onPress,
  testID,
  icon,
  dot,
  trailing,
  tone,
}: ChipProps) {
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
        borderColor: selected || tone === 'ask' ? 'transparent' : colors.border,
        // app.css .chip.ask comes after .chip.on, so the ask fill wins.
        backgroundColor:
          tone === 'ask' ? tint(colors.warn, 0.14) : selected ? colors.accentSoft : colors.surface,
      }}
    >
      {icon === undefined ? null : <Icon name={icon} size={16} color={colors.text} />}
      {dot === undefined ? null : (
        <View
          testID={dotId(testID)}
          style={{
            width: layout.stateDot,
            height: layout.stateDot,
            borderRadius: layout.stateDot / 2,
            backgroundColor: dot,
          }}
        />
      )}
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
      {trailing === 'chevron' ? <Icon name="down" size={16} color={colors.muted} /> : null}
    </Pressable>
  );
}

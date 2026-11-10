import { Pressable, Text, View } from 'react-native';
import { useTheme } from '../theme';
import { layout } from './layout';
import { optionCountId, optionId } from './testIds';

export interface SegmentedOption<T extends string> {
  value: T;
  label: string;
  // Shown after the label when given (0 included).
  count?: number;
}

export interface SegmentedProps<T extends string> {
  options: SegmentedOption<T>[];
  value: T;
  onChange: (value: T) => void;
  testID: string;
}

// The segmented control under a top bar (app.css .seg). Options draw 32 high and reach 44 with hitSlop.
export function Segmented<T extends string>({
  options,
  value,
  onChange,
  testID,
}: SegmentedProps<T>) {
  const theme = useTheme();
  const { colors } = theme;
  return (
    <View
      testID={testID}
      accessibilityRole="tablist"
      style={{
        flexDirection: 'row',
        flex: 1,
        padding: layout.segmentedPadding,
        borderRadius: theme.radius.md,
        backgroundColor: colors.surface2,
      }}
    >
      {options.map((o) => {
        const on = o.value === value;
        return (
          <Pressable
            key={o.value}
            testID={optionId(testID, o.value)}
            accessibilityRole="tab"
            accessibilityState={{ selected: on }}
            hitSlop={{ top: layout.compactSlop, bottom: layout.compactSlop }}
            onPress={() => onChange(o.value)}
            style={{
              flex: 1,
              height: layout.compact,
              flexDirection: 'row',
              gap: theme.space[1],
              alignItems: 'center',
              justifyContent: 'center',
              paddingHorizontal: theme.space[2],
              borderRadius: theme.radius.sm,
              backgroundColor: on ? colors.surface : 'transparent',
            }}
          >
            <Text
              numberOfLines={1}
              style={{
                color: on ? colors.text : colors.muted,
                fontSize: theme.font.size.base,
                fontWeight: theme.font.weight.medium,
              }}
            >
              {o.label}
            </Text>
            {o.count === undefined ? null : (
              <Text
                testID={optionCountId(testID, o.value)}
                style={{ color: colors.muted, fontSize: theme.font.size.sm }}
              >
                {o.count}
              </Text>
            )}
          </Pressable>
        );
      })}
    </View>
  );
}

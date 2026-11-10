import { Pressable, Text, View } from 'react-native';
import { useTheme } from '../theme';
import { actionId, primitiveIds } from './testIds';

export type BannerTone = 'info' | 'warn' | 'danger';

export interface BannerProps {
  tone: BannerTone;
  text: string;
  action?: { label: string; onPress: () => void };
  // Defaults to `banner`; the action is `${testID}-action`.
  testID?: string;
}

// A line across the top of a list: offline, a failed send, something to know (app.css .banner).
export function Banner({ tone, text, action, testID = primitiveIds.banner }: BannerProps) {
  const theme = useTheme();
  const { colors } = theme;
  const toneColour = colors[tone];
  return (
    <View
      testID={testID}
      accessibilityRole={tone === 'info' ? undefined : 'alert'}
      style={{
        flexDirection: 'row',
        alignItems: 'center',
        gap: theme.space[2],
        marginTop: theme.space[3],
        marginHorizontal: theme.space[4],
        paddingVertical: theme.space[1],
        paddingLeft: theme.space[3],
        paddingRight: action ? theme.space[1] : theme.space[3],
        minHeight: theme.touch.control,
        borderRadius: theme.radius.lg,
        borderWidth: 1,
        borderColor: colors.border,
        borderLeftWidth: 3,
        borderLeftColor: toneColour,
        backgroundColor: colors.surface,
      }}
    >
      <Text style={{ flex: 1, color: colors.text, fontSize: theme.font.size.base }}>{text}</Text>
      {action ? (
        <Pressable
          testID={actionId(testID)}
          accessibilityRole="button"
          onPress={action.onPress}
          style={{
            minHeight: theme.touch.control,
            paddingHorizontal: theme.space[2],
            justifyContent: 'center',
          }}
        >
          <Text
            style={{
              color: colors.accent,
              fontSize: theme.font.size.base,
              fontWeight: theme.font.weight.semibold,
            }}
          >
            {action.label}
          </Text>
        </Pressable>
      ) : null}
    </View>
  );
}

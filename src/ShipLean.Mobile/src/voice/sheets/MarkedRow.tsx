import { Text, View } from 'react-native';
import { useTheme } from '../../theme';
import { micPermissionIds } from './MicPermissionSheet.testIds';

export interface MarkedRowProps {
  label: string;
  // What to do on it: "Turn on", "Pick this".
  mark: string;
}

// The one row of the phone's settings to change, outlined and labelled.
export function MarkedRow({ label, mark }: MarkedRowProps) {
  const theme = useTheme();
  const { colors } = theme;
  return (
    <View
      testID={micPermissionIds.markedRow}
      style={{
        minHeight: theme.touch.row - 12,
        flexDirection: 'row',
        alignItems: 'center',
        gap: theme.space[2],
        paddingHorizontal: theme.space[3],
        borderRadius: theme.radius.sm,
        borderWidth: 2,
        borderColor: colors.accent,
        backgroundColor: colors.accentSoft,
      }}
    >
      <Text style={{ flex: 1, color: colors.text, fontSize: theme.font.size.base }}>{label}</Text>
      <Text
        style={{
          color: colors.accent,
          fontSize: theme.font.size.sm,
          fontWeight: theme.font.weight.semibold,
        }}
      >
        {mark}
      </Text>
    </View>
  );
}

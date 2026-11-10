import { Text, View } from 'react-native';
import { useTheme } from '../../theme';

export interface PhoneRowProps {
  label: string;
  detail?: string;
}

// One row of the phone's own settings, as drawn on the sheet.
export function PhoneRow({ label, detail }: PhoneRowProps) {
  const theme = useTheme();
  return (
    <View
      style={{
        minHeight: theme.touch.row - 12,
        justifyContent: 'center',
        paddingHorizontal: theme.space[3],
        borderRadius: theme.radius.sm,
        backgroundColor: theme.colors.surface2,
      }}
    >
      <Text style={{ color: theme.colors.text, fontSize: theme.font.size.base }}>{label}</Text>
      {detail === undefined ? null : (
        <Text style={{ color: theme.colors.muted, fontSize: theme.font.size.xs }}>{detail}</Text>
      )}
    </View>
  );
}

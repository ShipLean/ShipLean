import { Text, View } from 'react-native';
import { useTheme } from '../../theme';

export interface ChangeLineProps {
  text: string;
  // The muted line under it: why it stayed, or what was refused.
  detail?: string;
  testID: string;
}

// One change in the after-Apply sheets and banners: what the executor's describe() says, and a muted detail.
export function ChangeLine({ text, detail, testID }: ChangeLineProps) {
  const theme = useTheme();
  const { colors } = theme;
  return (
    <View testID={testID} style={{ gap: 2 }}>
      <Text style={{ color: colors.text, fontSize: theme.font.size.body15 }}>{text}</Text>
      {detail === undefined ? null : (
        <Text style={{ color: colors.muted, fontSize: theme.font.size.sm }}>{detail}</Text>
      )}
    </View>
  );
}

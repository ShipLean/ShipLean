import { Text, View } from 'react-native';
import { useTheme } from '../../theme';
import { micPermissionIds } from './MicPermissionSheet.testIds';
import { MarkedRow } from './MarkedRow';
import { PhoneRow } from './PhoneRow';

// Android's ShipLean app info › Permissions › Microphone, drawn small: the first option is the row to pick.
export function AppInfoDrawing() {
  const theme = useTheme();
  return (
    <View
      testID={micPermissionIds.appInfoDrawing}
      accessibilityLabel="App info, Permissions, Microphone: pick Allow only while using the app"
      style={{ gap: theme.space[2] }}
    >
      <Text style={{ color: theme.colors.muted, fontSize: theme.font.size.sm }}>
        ShipLean app info › Permissions › Microphone
      </Text>
      <MarkedRow label="Allow only while using the app" mark="Pick this" />
      <PhoneRow label="Ask every time" />
      <PhoneRow label="Don’t allow" detail="Chosen now" />
    </View>
  );
}

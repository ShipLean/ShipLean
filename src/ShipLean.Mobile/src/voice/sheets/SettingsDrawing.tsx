import { Text, View } from 'react-native';
import { useTheme } from '../../theme';
import { micPermissionIds } from './MicPermissionSheet.testIds';
import { MarkedRow } from './MarkedRow';
import { PhoneRow } from './PhoneRow';

// iPhone Settings › ShipLean, drawn small: the Microphone switch is the row to change.
export function SettingsDrawing() {
  const theme = useTheme();
  return (
    <View
      testID={micPermissionIds.settingsDrawing}
      accessibilityLabel="Settings, ShipLean, Microphone switch: turn on"
      style={{ gap: theme.space[2] }}
    >
      <Text style={{ color: theme.colors.muted, fontSize: theme.font.size.sm }}>
        Settings › ShipLean
      </Text>
      <PhoneRow label="ShipLean" detail="Allow ShipLean to access" />
      <MarkedRow label="Microphone" mark="Turn on" />
      <PhoneRow label="Notifications" detail="Banners, Sounds, Badges" />
    </View>
  );
}

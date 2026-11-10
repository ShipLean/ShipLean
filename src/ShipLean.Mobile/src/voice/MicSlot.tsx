import { View } from 'react-native';
import { useTheme } from '../theme';
import { layout } from '../ui/layout';
import { micSlotId, type MicPlacement } from './MicSlot.testIds';
import { useVoice } from './VoiceProvider';

export interface MicSlotProps {
  placement: MicPlacement;
}

// The mic in the tab bar's centre and in the dock.
// TODO: hold to talk, the level ring and the disabled reasons replace this plain circle.
export function MicSlot({ placement }: MicSlotProps) {
  const theme = useTheme();
  const { state } = useVoice();
  return (
    <View
      testID={micSlotId(placement)}
      accessibilityRole="button"
      accessibilityLabel="Microphone"
      accessibilityState={{ disabled: state.kind === 'disabled' }}
      style={{
        width: layout.dockMic,
        height: layout.dockMic,
        borderRadius: layout.dockMic / 2,
        backgroundColor: theme.colors.surface2,
      }}
    />
  );
}

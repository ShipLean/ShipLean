import type { FunctionComponent } from 'react';
import { Text, View } from 'react-native';
import { useTheme } from '../theme';
import { BottomSheet } from '../ui/BottomSheet';
import { Button } from '../ui/Button';
import { engines } from '../voice/engines';
import { formatGb } from './modelChoice';
import { mobileDataSheetIds } from './MobileDataSheet.testIds';

export interface MobileDataSheetProps {
  open: boolean;
  // Bytes still to download.
  leftBytes: number;
  onClose: () => void;
}

// Setup D7: Wi-Fi dropped and the download paused. Waiting is the default; mobile data is used only for this download.
export const MobileDataSheet: FunctionComponent<MobileDataSheetProps> = ({
  open,
  leftBytes,
  onClose,
}) => {
  const theme = useTheme();
  return (
    <BottomSheet
      testID={mobileDataSheetIds.root}
      title="Continue on mobile data?"
      open={open}
      onClose={onClose}
    >
      <View style={{ gap: theme.space[3] }}>
        <Text
          testID={mobileDataSheetIds.body}
          style={{ color: theme.colors.text, fontSize: theme.font.size.body15 }}
        >
          {`You're off Wi-Fi. ${formatGb(leftBytes)} of the voice models is left to download.`}
        </Text>
        <Button testID={mobileDataSheetIds.wait} label="Wait for Wi-Fi" onPress={onClose} />
        <Button
          testID={mobileDataSheetIds.useMobileData}
          label="Use mobile data for this download"
          variant="ghost"
          onPress={() => {
            engines().models.useMobileDataOnce();
            onClose();
          }}
        />
        <Text style={{ color: theme.colors.muted, fontSize: theme.font.size.base }}>
          Change the default in Settings › Voice
        </Text>
      </View>
    </BottomSheet>
  );
};

import type { FunctionComponent } from 'react';
import { Text, View } from 'react-native';
import { useTheme } from '../theme';
import { BottomSheet } from '../ui/BottomSheet';
import { Button } from '../ui/Button';
import { engines } from '../voice/engines';
import type { MemoryView } from './bannerView';
import { formatGb } from './modelChoice';
import { memorySheetIds } from './MemorySheet.testIds';

export interface MemorySheetProps {
  open: boolean;
  view: MemoryView;
  onClose: () => void;
}

// P-S3: the command model failed its memory check, so voice is off. The smaller model is offered, never downloaded
// by itself.
export const MemorySheet: FunctionComponent<MemorySheetProps> = ({ open, view, onClose }) => {
  const theme = useTheme();
  const smaller = view.smaller;
  return (
    <BottomSheet
      testID={memorySheetIds.root}
      title="Not enough memory"
      open={open}
      onClose={onClose}
    >
      <View style={{ gap: theme.space[3] }}>
        <Text
          testID={memorySheetIds.body}
          style={{ color: theme.colors.text, fontSize: theme.font.size.body15 }}
        >
          {`${view.failed} needs more memory than this phone has free. Voice is off.`}
        </Text>
        {smaller === null ? null : (
          <Button
            testID={memorySheetIds.switchSmaller}
            label={`Switch to ${smaller.name} (${formatGb(smaller.bytes)})`}
            onPress={() => {
              const models = engines().models;
              models.choose(smaller.model);
              models.download();
              onClose();
            }}
          />
        )}
        <Button testID={memorySheetIds.notNow} label="Not now" variant="ghost" onPress={onClose} />
      </View>
    </BottomSheet>
  );
};

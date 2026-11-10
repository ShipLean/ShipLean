import { useState } from 'react';
import { View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useTheme } from '../theme';
import { layout } from '../ui/layout';
import { Toast } from '../ui/Toast';
import { undoToastId } from './UndoToastHost.testIds';
import { useUndoToast } from './useUndoToast';

// The runner's undo toast: the newest batch whose undo window is open, with Undo. It goes when the window closes.
// TODO: after Undo, say "Undone", or open the partial sheet that lists what was undone and what stayed.
export function UndoToastHost() {
  const theme = useTheme();
  const insets = useSafeAreaInsets();
  const toast = useUndoToast();
  const [dismissed, setDismissed] = useState<string | null>(null);
  if (toast === null || toast.batchId === dismissed) return null;
  return (
    <View
      testID={undoToastId}
      pointerEvents="box-none"
      style={{
        position: 'absolute',
        left: theme.space[4],
        right: theme.space[4],
        bottom: insets.bottom + layout.tabBar + layout.toastGap,
      }}
    >
      <Toast
        text={toast.text}
        action={{ label: 'Undo', onPress: () => void toast.undo() }}
        onDismiss={() => setDismissed(toast.batchId)}
      />
    </View>
  );
}

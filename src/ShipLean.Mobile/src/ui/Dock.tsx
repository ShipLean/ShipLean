import { View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { DockComposer } from '../dock/DockComposer';
import { useTheme } from '../theme';
import { MicSlot } from '../voice/MicSlot';
import { layout } from './layout';
import { primitiveIds } from './testIds';

export interface DockProps {
  // The work item a note goes on; none on screens that aren't one item.
  itemId?: string;
  // The key of that item; the note action needs it.
  itemKey?: string;
  // The item's project; the members for mentions load by project.
  projectId?: string;
  placeholder: string;
  // Called after a note is sent.
  onSubmitted?: () => void;
}

// The dock at the foot of a detail screen (app.css .dock): the "Type a note" field and the mic. Pressing the field
// opens the composer (src/dock): a note posts at once through the runner; Command goes through the voice preview.
export function Dock({ itemId, itemKey, projectId, placeholder, onSubmitted }: DockProps) {
  const theme = useTheme();
  const insets = useSafeAreaInsets();
  const { colors } = theme;
  return (
    <View
      testID={primitiveIds.dock}
      style={{
        paddingTop: layout.dockPaddingTop,
        paddingHorizontal: theme.space[4],
        paddingBottom: insets.bottom + layout.dockPaddingBottom,
        backgroundColor: colors.surface,
        borderTopWidth: 1,
        borderTopColor: colors.border,
      }}
    >
      <DockComposer
        itemId={itemId}
        itemKey={itemKey}
        projectId={projectId}
        placeholder={placeholder}
        onSubmitted={onSubmitted}
        mic={
          <View testID={primitiveIds.dockMic}>
            <MicSlot placement="dock" />
          </View>
        }
      />
    </View>
  );
}

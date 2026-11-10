import { TextInput, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
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

// The dock at the foot of a detail screen (app.css .dock): the "Type a note" field and the mic.
// TODO: typing and sending a note, and the Note | Command switch, replace this stub field.
export function Dock({ placeholder }: DockProps) {
  const theme = useTheme();
  const insets = useSafeAreaInsets();
  const { colors } = theme;
  return (
    <View
      testID={primitiveIds.dock}
      style={{
        flexDirection: 'row',
        alignItems: 'center',
        gap: layout.dockGap,
        paddingTop: layout.dockPaddingTop,
        paddingHorizontal: theme.space[4],
        paddingBottom: insets.bottom + layout.dockPaddingBottom,
        backgroundColor: colors.surface,
        borderTopWidth: 1,
        borderTopColor: colors.border,
      }}
    >
      <TextInput
        testID={primitiveIds.dockField}
        // Read-only until typing is built, so nothing typed is dropped.
        editable={false}
        placeholder={placeholder}
        placeholderTextColor={colors.muted}
        accessibilityLabel={placeholder}
        style={{
          flex: 1,
          height: theme.touch.control,
          borderRadius: layout.dockFieldRadius,
          paddingHorizontal: theme.space[4],
          backgroundColor: colors.surface2,
          color: colors.text,
          fontSize: theme.font.size.body15,
        }}
      />
      <View testID={primitiveIds.dockMic}>
        <MicSlot placement="dock" />
      </View>
    </View>
  );
}

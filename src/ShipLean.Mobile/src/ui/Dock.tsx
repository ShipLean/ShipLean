import { TextInput, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useTheme } from '../theme';
import { layout } from './layout';
import { primitiveIds } from './testIds';

export interface DockProps {
  // The work item a note goes on; none on screens that aren't one item.
  itemId?: string;
  placeholder: string;
  // Called after a note is sent.
  onSubmitted?: () => void;
}

// The dock at the foot of a detail screen (app.css .dock): the "Type a note" field and the mic.
// TODO: typing and sending a note, and the Note | Command switch, replace this stub field; the real mic replaces
// the greyed placeholder.
export function Dock({ placeholder }: DockProps) {
  const theme = useTheme();
  const insets = useSafeAreaInsets();
  const { colors } = theme;
  const sizes = layout(theme);
  return (
    <View
      testID={primitiveIds.dock}
      style={{
        flexDirection: 'row',
        alignItems: 'center',
        gap: theme.space[2] + 2,
        paddingTop: theme.space[2] + 2,
        paddingHorizontal: theme.space[4],
        paddingBottom: insets.bottom + theme.space[1],
        backgroundColor: colors.surface,
        borderTopWidth: 1,
        borderTopColor: colors.border,
      }}
    >
      <TextInput
        testID={primitiveIds.dockField}
        placeholder={placeholder}
        placeholderTextColor={colors.muted}
        accessibilityLabel={placeholder}
        style={{
          flex: 1,
          height: theme.touch.control,
          borderRadius: theme.touch.control / 2,
          paddingHorizontal: theme.space[4],
          backgroundColor: colors.surface2,
          color: colors.text,
          fontSize: theme.font.size.body15,
        }}
      />
      <View
        testID={primitiveIds.dockMic}
        accessibilityLabel="Microphone"
        accessibilityState={{ disabled: true }}
        style={{
          width: sizes.dockMic,
          height: sizes.dockMic,
          borderRadius: sizes.dockMic / 2,
          backgroundColor: colors.surface2,
        }}
      />
    </View>
  );
}

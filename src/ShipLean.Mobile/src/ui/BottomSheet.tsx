import type { ReactNode } from 'react';
import { Modal, Pressable, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useTheme } from '../theme';
import { layout } from './layout';
import { primitiveIds, sheetTitleId } from './testIds';

export interface BottomSheetProps {
  title: string;
  open: boolean;
  onClose: () => void;
  children: ReactNode;
  // The sheet's id; its title is `${testID}-title`.
  testID: string;
}

// A sheet over the screen (app.css .sheet) on React Native's Modal: tapping the backdrop or Back closes it.
export function BottomSheet({ title, open, onClose, children, testID }: BottomSheetProps) {
  const theme = useTheme();
  const insets = useSafeAreaInsets();
  const { colors } = theme;
  return (
    <Modal visible={open} transparent animationType="slide" onRequestClose={onClose}>
      <View style={{ flex: 1, justifyContent: 'flex-end' }}>
        <Pressable
          testID={primitiveIds.sheetBackdrop}
          accessibilityRole="button"
          accessibilityLabel="Close"
          onPress={onClose}
          style={{
            position: 'absolute',
            top: 0,
            left: 0,
            right: 0,
            bottom: 0,
            backgroundColor: colors.scrim,
          }}
        />
        <View
          testID={testID}
          style={{
            backgroundColor: colors.surface,
            borderTopLeftRadius: layout.sheetRadius,
            borderTopRightRadius: layout.sheetRadius,
            paddingTop: theme.space[2],
            paddingHorizontal: theme.space[4],
            paddingBottom: insets.bottom + theme.space[3],
          }}
        >
          <View
            style={{
              alignSelf: 'center',
              width: layout.grab.width,
              height: layout.grab.height,
              borderRadius: layout.grab.height / 2,
              marginBottom: layout.grabGap,
              backgroundColor: colors.border,
            }}
          />
          <Text
            testID={sheetTitleId(testID)}
            accessibilityRole="header"
            style={{
              color: colors.text,
              fontSize: theme.font.size.lg,
              fontWeight: theme.font.weight.semibold,
              marginBottom: theme.space[1],
            }}
          >
            {title}
          </Text>
          {children}
        </View>
      </View>
    </Modal>
  );
}

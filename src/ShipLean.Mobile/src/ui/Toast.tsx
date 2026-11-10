import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react';
import { Pressable, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useTheme } from '../theme';
import { layout } from './layout';
import { primitiveIds } from './testIds';

export interface ToastAction {
  label: string;
  onPress: () => void;
}

export interface ToastOptions {
  text: string;
  action?: ToastAction;
  // How long it stays up.
  ms: number;
}

export interface ToastProps {
  text: string;
  action?: ToastAction;
  onDismiss: () => void;
}

// The toast itself (app.css .toast): dark on light, a line of text and at most one action. The action dismisses it.
export function Toast({ text, action, onDismiss }: ToastProps) {
  const theme = useTheme();
  const { colors } = theme;
  return (
    <View
      testID={primitiveIds.toast}
      accessibilityRole="alert"
      accessibilityLiveRegion="polite"
      style={{
        minHeight: theme.touch.control + theme.space[1],
        flexDirection: 'row',
        alignItems: 'center',
        gap: theme.space[2],
        paddingLeft: theme.space[3] + 2,
        paddingRight: theme.space[2],
        borderRadius: theme.radius.lg + 2,
        backgroundColor: colors.text,
      }}
    >
      <Text style={{ flex: 1, color: colors.bg, fontSize: theme.font.size.base }}>{text}</Text>
      {action ? (
        <Pressable
          testID={primitiveIds.toastAction}
          accessibilityRole="button"
          onPress={() => {
            onDismiss();
            action.onPress();
          }}
          style={{
            minHeight: theme.touch.control,
            paddingHorizontal: theme.space[2] + 2,
            justifyContent: 'center',
          }}
        >
          <Text
            style={{
              color: colors.accentSoft,
              fontSize: theme.font.size.base,
              fontWeight: theme.font.weight.semibold,
            }}
          >
            {action.label}
          </Text>
        </Pressable>
      ) : null}
    </View>
  );
}

interface ToastApi {
  show: (options: ToastOptions) => void;
}

const ToastContext = createContext<ToastApi | null>(null);

export function useToast(): ToastApi {
  const api = useContext(ToastContext);
  if (api === null) throw new Error('useToast needs a ToastHost above it');
  return api;
}

interface Shown extends ToastOptions {
  // A new show() replaces the toast and restarts its timer, even with the same text.
  seq: number;
}

// Holds one toast at a time and shows it above the tab bar; a new one replaces the one showing.
export function ToastHost({ children }: { children: ReactNode }) {
  const theme = useTheme();
  const insets = useSafeAreaInsets();
  const [shown, setShown] = useState<Shown | null>(null);
  const show = useCallback(
    (options: ToastOptions) => setShown((prev) => ({ ...options, seq: (prev?.seq ?? 0) + 1 })),
    [],
  );
  const dismiss = useCallback(() => setShown(null), []);
  const api = useMemo(() => ({ show }), [show]);

  useEffect(() => {
    if (shown === null) return;
    const timer = setTimeout(dismiss, shown.ms);
    return () => clearTimeout(timer);
  }, [shown, dismiss]);

  return (
    <ToastContext.Provider value={api}>
      <View style={{ flex: 1 }}>
        {children}
        {shown ? (
          <View
            pointerEvents="box-none"
            style={{
              position: 'absolute',
              left: theme.space[4],
              right: theme.space[4],
              bottom: insets.bottom + layout(theme).tabBar + theme.space[2],
            }}
          >
            <Toast key={shown.seq} text={shown.text} action={shown.action} onDismiss={dismiss} />
          </View>
        ) : null}
      </View>
    </ToastContext.Provider>
  );
}

import { useRouter } from 'expo-router';
import { useRef, useState } from 'react';
import { Text, View, type GestureResponderEvent } from 'react-native';
import { links } from '../links';
import { useTheme } from '../theme';
import { layout } from '../ui/layout';
import type { DisabledReason } from './internals';
import { micLabelId, micSlotId, micStrikeId, type MicPlacement } from './MicSlot.testIds';
import { MicPermissionSheet } from './sheets/MicPermissionSheet';
import { useVoice } from './VoiceProvider';

export interface MicSlotProps {
  placement: MicPlacement;
}

// How far a finger may wander outside the mic before the hold counts as dragged off.
const DRAG_OFF_SLOP = 24;

const DISABLED_LABEL: Record<DisabledReason, string> = {
  skipped: 'Set up voice',
  noModels: 'Set up voice',
  downloading: 'Downloading',
  micOff: 'Mic off',
};

// The mic in the tab bar's centre and in the dock. Hold to talk: press starts listening and release ends it;
// dragging off cancels. When voice can't run it says why and a tap leads to the fix, so it is never a dead button.
export function MicSlot({ placement }: MicSlotProps) {
  const theme = useTheme();
  const router = useRouter();
  const voice = useVoice();
  const { state } = voice;
  const [permissionOpen, setPermissionOpen] = useState(false);
  // What the current touch is doing: nothing more to do on release (a hold that opened a sheet or was dragged off).
  const spent = useRef(false);
  const holding = useRef(false);

  const micOff = state.kind === 'disabled' && state.reason === 'micOff';
  const asking = state.kind === 'idle' && state.permission === 'undetermined';
  const label = state.kind === 'disabled' ? DISABLED_LABEL[state.reason] : 'Hold to talk';
  const inert = state.kind === 'disabled';
  const listening = state.kind === 'listening';

  const leadToFix = () => {
    if (state.kind !== 'disabled') return;
    switch (state.reason) {
      case 'skipped':
      case 'noModels':
        router.push(links.setup('models'));
        return;
      case 'downloading':
        router.push(links.setup('downloading'));
        return;
      case 'micOff':
        setPermissionOpen(true);
        return;
    }
  };

  const onGrant = () => {
    spent.current = false;
    if (micOff || asking) {
      spent.current = true;
      setPermissionOpen(true);
      return;
    }
    if (inert) return;
    holding.current = true;
    voice.holdStart();
  };

  const onMove = (e: GestureResponderEvent) => {
    if (!holding.current) return;
    const { locationX, locationY } = e.nativeEvent;
    const size = layout.dockMic;
    const outside =
      locationX < -DRAG_OFF_SLOP ||
      locationY < -DRAG_OFF_SLOP ||
      locationX > size + DRAG_OFF_SLOP ||
      locationY > size + DRAG_OFF_SLOP;
    if (outside) {
      holding.current = false;
      spent.current = true;
      voice.cancel();
    }
  };

  const onRelease = () => {
    if (holding.current) {
      holding.current = false;
      voice.holdEnd();
      return;
    }
    if (!spent.current && inert) leadToFix();
    spent.current = false;
  };

  // The system took the touch (a call, a gesture): the hold ends without sending anything.
  const onTerminate = () => {
    if (holding.current) {
      holding.current = false;
      voice.cancel();
    }
    spent.current = false;
  };

  const { colors } = theme;
  const background = listening ? colors.accent : inert ? colors.surface2 : colors.accentSoft;
  const foreground = listening ? colors.accentFg : inert ? colors.muted : colors.accent;
  return (
    <>
      <View
        testID={micSlotId(placement)}
        accessibilityRole="button"
        accessibilityLabel={label}
        accessibilityState={{ disabled: inert && !micOff }}
        onAccessibilityTap={inert ? leadToFix : undefined}
        onStartShouldSetResponder={() => true}
        onResponderGrant={onGrant}
        onResponderMove={onMove}
        onResponderRelease={onRelease}
        onResponderTerminate={onTerminate}
        style={{
          width: layout.dockMic,
          height: layout.dockMic,
          borderRadius: layout.dockMic / 2,
          alignItems: 'center',
          justifyContent: 'center',
          backgroundColor: background,
          opacity: inert ? 0.7 : 1,
        }}
      >
        <Text
          testID={micLabelId(placement)}
          style={{
            color: foreground,
            fontSize: theme.font.size.xxs,
            fontWeight: theme.font.weight.semibold,
            textAlign: 'center',
          }}
        >
          {label}
        </Text>
        {micOff ? (
          <View
            testID={micStrikeId(placement)}
            pointerEvents="none"
            style={{
              position: 'absolute',
              width: layout.dockMic,
              height: 2,
              backgroundColor: colors.muted,
              transform: [{ rotate: '-45deg' }],
            }}
          />
        ) : null}
      </View>
      <MicPermissionSheet
        open={permissionOpen}
        status={state.kind === 'idle' || state.kind === 'disabled' ? state.permission : 'granted'}
        onClose={() => setPermissionOpen(false)}
        onTypeInstead={() => {
          setPermissionOpen(false);
          voice.typeInstead();
        }}
      />
    </>
  );
}

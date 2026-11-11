import type { FeatureHealth } from '@shiplean/core/api/screens';
import { Text, View } from 'react-native';
import { useTheme } from '../theme';
import { layout } from './layout';
import { primitiveIds } from './testIds';

export interface HealthPillProps {
  health: FeatureHealth | null;
  // Show "On track" too (the Features board). Off by default: the Backlog shows health only when it is not on track.
  showOnTrack?: boolean;
}

const LABELS = { onTrack: 'On track', atRisk: 'At risk', blocked: 'Blocked' } as const;

// A Feature's health (app.css .health). At risk and Blocked always show; on track only with showOnTrack.
export function HealthPill({ health, showOnTrack = false }: HealthPillProps) {
  const theme = useTheme();
  if (health === null || (health === 'onTrack' && !showOnTrack)) return null;
  const colour = {
    onTrack: theme.colors.ok,
    atRisk: theme.colors.warn,
    blocked: theme.colors.danger,
  }[health];
  return (
    <View
      testID={primitiveIds.healthPill}
      style={{ flexDirection: 'row', alignItems: 'center', gap: layout.stateGap }}
    >
      <View
        style={{
          width: layout.healthDot,
          height: layout.healthDot,
          borderRadius: layout.healthDot / 2,
          backgroundColor: colour,
        }}
      />
      <Text
        style={{
          color: colour,
          fontSize: theme.font.size.sm,
          fontWeight: theme.font.weight.semibold,
        }}
      >
        {LABELS[health]}
      </Text>
    </View>
  );
}

import { Text, View } from 'react-native';
import { useTheme } from '../theme';
import { primitiveIds } from './testIds';

// TODO: import FeatureHealth from @shiplean/core/api/screens once it lands there.
export type FeatureHealth = 'onTrack' | 'atRisk' | 'blocked';

export interface HealthPillProps {
  health: FeatureHealth | null;
}

const LABELS = { atRisk: 'At risk', blocked: 'Blocked' } as const;

// A Feature's health (app.css .health). Only At risk and Blocked show, on every screen; on track shows nothing.
export function HealthPill({ health }: HealthPillProps) {
  const theme = useTheme();
  if (health === null || health === 'onTrack') return null;
  const colour = health === 'blocked' ? theme.colors.danger : theme.colors.warn;
  return (
    <View
      testID={primitiveIds.healthPill}
      style={{ flexDirection: 'row', alignItems: 'center', gap: theme.space[1] + 1 }}
    >
      <View
        style={{
          width: theme.space[2],
          height: theme.space[2],
          borderRadius: theme.space[1],
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

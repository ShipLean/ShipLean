import type { FeatureRowDto } from '@shiplean/core/api/featuresOverview';
import type { FeatureHealth } from '@shiplean/core/api/screens';
import { Pressable, Text } from 'react-native';
import { useTheme } from '../../theme';
import { BottomSheet } from '../../ui/BottomSheet';
import { useFeaturesActions } from '../FeaturesActions';
import { featureHealthOptionId, featureHealthSheetId } from '../testIds';

export interface HealthSheetProps {
  row: FeatureRowDto;
  projectId: string;
  open: boolean;
  onClose: () => void;
}

const OPTIONS: { health: FeatureHealth | null; id: string; label: string }[] = [
  { health: 'onTrack', id: 'onTrack', label: 'On track' },
  { health: 'atRisk', id: 'atRisk', label: 'At risk' },
  { health: 'blocked', id: 'blocked', label: 'Blocked' },
  { health: null, id: 'notSet', label: 'Not set' },
];

// A Feature's health is set by hand (D2), never worked out. A pick sends at once, as a tap does.
export function HealthSheet({ row, projectId, open, onClose }: HealthSheetProps) {
  const theme = useTheme();
  const { colors } = theme;
  const { dispatch } = useFeaturesActions();
  return (
    <BottomSheet
      title={`${row.key} health`}
      open={open}
      onClose={onClose}
      testID={featureHealthSheetId(row.key)}
    >
      {OPTIONS.map((option) => {
        const current = option.health === row.health;
        return (
          <Pressable
            key={option.id}
            testID={featureHealthOptionId(option.id)}
            accessibilityRole="button"
            accessibilityState={{ selected: current }}
            onPress={() => {
              if (!current)
                dispatch({
                  area: 'features',
                  kind: 'setHealth',
                  projectId,
                  itemId: row.id,
                  key: row.key,
                  revision: row.revision,
                  health: option.health,
                  from: row.health,
                });
              onClose();
            }}
            style={{
              minHeight: theme.touch.row,
              flexDirection: 'row',
              alignItems: 'center',
              borderBottomWidth: 1,
              borderBottomColor: colors.border,
            }}
          >
            <Text
              style={{
                flex: 1,
                color: colors.text,
                fontSize: theme.font.size.md,
                fontWeight: current ? theme.font.weight.bold : theme.font.weight.regular,
              }}
            >
              {option.label}
            </Text>
            {current ? (
              <Text style={{ color: colors.accent, fontSize: theme.font.size.md }}>✓</Text>
            ) : null}
          </Pressable>
        );
      })}
    </BottomSheet>
  );
}

import { RETRO_CATEGORIES, type RetroCategory } from '@shiplean/core/api/retros';
import { Pressable, Text, View } from 'react-native';
import { useTheme } from '../../theme';
import { tint } from '../../ui/layout';
import {
  retroIds,
  retroTileCountId,
  retroTileId,
  retroTileStubId,
  retroTileToDecideId,
} from '../testIds';
import { CATEGORY_LABELS } from './notesModel';

export interface CategoryTilesProps {
  // Each category's point count and how many are left to decide.
  counts: Record<RetroCategory, { points: number; toDecide: number }>;
  selected: RetroCategory;
  onSelect: (category: RetroCategory) => void;
}

// retro.html .cats: four tiles in the web's order, each in its category's colour.
export function CategoryTiles({ counts, selected, onSelect }: CategoryTilesProps) {
  const theme = useTheme();
  const { colors } = theme;
  const colour: Record<RetroCategory, string> = {
    wentWell: colors.ok,
    wentBadly: colors.danger,
    improve: colors.accent,
    notInPlan: colors.purple,
  };
  return (
    <View
      testID={retroIds.tiles}
      style={{ flexDirection: 'row', gap: theme.space[2], padding: theme.space[4] }}
    >
      {RETRO_CATEGORIES.map((category) => {
        const on = category === selected;
        const { points, toDecide } = counts[category];
        return (
          <Pressable
            key={category}
            testID={retroTileId(category)}
            accessibilityRole="button"
            accessibilityState={{ selected: on }}
            onPress={() => onSelect(category)}
            style={{
              flex: 1,
              minHeight: 84,
              gap: 2,
              padding: theme.space[2],
              borderRadius: theme.radius.md,
              borderWidth: on ? 2 : 1,
              borderColor: on ? colour[category] : colors.border,
              backgroundColor: on ? tint(colour[category], 0.12) : colors.surface,
            }}
          >
            <View
              testID={retroTileStubId(category)}
              style={{
                width: 18,
                height: 4,
                borderRadius: 2,
                marginBottom: 4,
                backgroundColor: colour[category],
              }}
            />
            <Text
              testID={retroTileCountId(category)}
              style={{
                fontSize: theme.font.size.lg,
                fontWeight: theme.font.weight.semibold,
                color: colors.text,
              }}
            >
              {points}
            </Text>
            <Text style={{ fontSize: theme.font.size.xs, color: on ? colors.text : colors.muted }}>
              {CATEGORY_LABELS[category]}
            </Text>
            {toDecide > 0 ? (
              <Text
                testID={retroTileToDecideId(category)}
                style={{
                  marginTop: 'auto',
                  fontSize: theme.font.size.xxs,
                  fontWeight: theme.font.weight.semibold,
                  color: colors.warn,
                }}
              >
                {toDecide} to decide
              </Text>
            ) : null}
          </Pressable>
        );
      })}
    </View>
  );
}

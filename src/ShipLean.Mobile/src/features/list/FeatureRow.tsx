import type { FeatureRowDto } from '@shiplean/core/api/featuresOverview';
import { Pressable, Text, View } from 'react-native';
import { useTheme } from '../../theme';
import { HealthPill } from '../../ui/HealthPill';
import { layout, tint } from '../../ui/layout';
import { NeedsPill } from '../../ui/NeedsPill';
import { countStoryLevelUnder } from '../../ui/progress';
import { ProgressBar } from '../../ui/ProgressBar';
import { FeatureDetails } from '../details/FeatureDetails';
import {
  featureMovedId,
  featureRowExpandId,
  featureRollupId,
  featureRowId,
  featureStaleId,
  featureStatusId,
  featureWhoId,
} from '../testIds';
import { rollupText, statusBy, type RankMove } from './filters';

export interface FeatureRowProps {
  row: FeatureRowDto;
  projectId: string;
  projectKey: string;
  expanded: boolean;
  onToggle: () => void;
  // Set for 3 s on a row whose rank changed since the last read.
  highlight: RankMove | null;
}

// One Feature (feature-board.html .frow): key, health, Needs you, title, status line with who and how old, progress and
// roll-ups. A tap opens it in place into FeatureDetails.
export function FeatureRow({
  row,
  projectId,
  projectKey,
  expanded,
  onToggle,
  highlight,
}: FeatureRowProps) {
  const theme = useTheme();
  const { colors } = theme;
  const small = { fontSize: theme.font.size.sm, color: colors.muted } as const;
  const counts = countStoryLevelUnder(row.rollups);
  const roll = rollupText(row.rollups);
  return (
    <View
      testID={featureRowId(row.key)}
      style={{
        backgroundColor: highlight ? tint(colors.accent, 0.12) : colors.surface,
        borderBottomWidth: 1,
        borderBottomColor: colors.border,
      }}
    >
      <Pressable
        testID={featureRowExpandId(row.key)}
        accessibilityRole="button"
        accessibilityState={{ expanded }}
        onPress={onToggle}
        style={{
          minHeight: layout.listRow,
          paddingVertical: theme.space[3],
          paddingHorizontal: theme.space[4],
          gap: theme.space[1],
        }}
      >
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: theme.space[2] }}>
          <Text style={{ ...small, fontFamily: theme.font.mono }}>{row.key}</Text>
          <HealthPill health={row.health} />
          {highlight ? (
            <Text
              testID={featureMovedId(row.key)}
              style={{
                color: colors.accent,
                fontSize: theme.font.size.sm,
                fontWeight: theme.font.weight.semibold,
              }}
            >
              {highlight.up ? '↑' : '↓'} from {highlight.from}
            </Text>
          ) : null}
          <View style={{ flex: 1 }} />
          <NeedsPill needs={row.needsYou} />
        </View>
        <Text
          numberOfLines={2}
          style={{
            color: colors.text,
            fontSize: theme.font.size.body15,
            fontWeight: theme.font.weight.semibold,
          }}
        >
          {row.title}
        </Text>
        {row.statusLine === null || row.statusLine === '' ? (
          <Text testID={featureStatusId(row.key)} style={small}>
            No status yet.
          </Text>
        ) : (
          <>
            <Text
              testID={featureStatusId(row.key)}
              numberOfLines={2}
              style={{ color: colors.text, fontSize: theme.font.size.sm }}
            >
              {row.statusLine}
            </Text>
            <View style={{ flexDirection: 'row', alignItems: 'center', gap: theme.space[1] }}>
              <Text
                testID={featureWhoId(row.key)}
                style={{
                  fontSize: theme.font.size.xs,
                  color: row.statusStale ? colors.warn : colors.muted,
                  fontWeight: row.statusStale ? theme.font.weight.semibold : undefined,
                }}
              >
                {statusBy(row)}
              </Text>
              {row.statusStale ? (
                <Text
                  testID={featureStaleId(row.key)}
                  style={{
                    fontSize: theme.font.size.xs,
                    color: colors.warn,
                    fontWeight: theme.font.weight.semibold,
                  }}
                >
                  Stale
                </Text>
              ) : null}
            </View>
          </>
        )}
        <View
          style={{
            flexDirection: 'row',
            alignItems: 'center',
            gap: theme.space[3],
            marginTop: theme.space[2],
          }}
        >
          <View style={{ flex: 1 }}>
            {counts.total > 0 ? <ProgressBar rollups={row.rollups} /> : null}
          </View>
          <Text
            testID={featureRollupId(row.key)}
            style={{ ...small, fontSize: theme.font.size.xs }}
          >
            {roll === '' ? 'No stories yet' : roll}
          </Text>
        </View>
      </Pressable>
      {expanded ? (
        <View style={{ paddingHorizontal: theme.space[4], paddingBottom: theme.space[3] }}>
          <FeatureDetails row={row} projectId={projectId} projectKey={projectKey} />
        </View>
      ) : null}
    </View>
  );
}

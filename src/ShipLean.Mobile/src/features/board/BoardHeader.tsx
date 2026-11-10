import type { StatusVerifiedDto, WorkItemDto } from '@shiplean/core/api/workItems';
import type { FeatureRowDto } from '@shiplean/core/api/featuresOverview';
import type { FeatureBoardHeaderDto } from '@shiplean/core/api/views';
import { platform } from '@shiplean/core/platform';
import { openBrowserAsync } from 'expo-web-browser';
import { Pressable, Text, View } from 'react-native';
import { webUrl } from '../../links';
import { useTheme } from '../../theme';
import { HealthPill } from '../../ui/HealthPill';
import { layout } from '../../ui/layout';
import { ProgressBar } from '../../ui/ProgressBar';
import { StatePill } from '../../ui/StatePill';
import { featureBoardIds } from '../testIds';
import { pointsPercent, type BoardFigures, type Fraction } from './boardModel';

export interface BoardHeaderProps {
  projectKey: string;
  featureKey: string;
  header: FeatureBoardHeaderDto;
  figures: BoardFigures;
  // The Feature's own read: health, status line and who verified it (the board header has none).
  item: WorkItemDto | undefined;
  // The Feature's row of the overview: its roll-ups draw the bar. Undefined while the overview is not loaded.
  row: FeatureRowDto | undefined;
  // True once the overview read has settled, so a missing row means the Feature is folded away (Closed).
  overviewSettled: boolean;
}

function statusBy(verified: StatusVerifiedDto): string {
  const who = verified.agent === null ? (verified.by?.name ?? '') : `${verified.agent} (agent)`;
  return who === '' ? verified.atLabel : `${who} · ${verified.atLabel}`;
}

const text = (f: Fraction) => `${f.closed}/${f.total}`;

// The Feature's summary (feature-board.html .sum): state and health, the status line with who and when, four figures,
// the progress bar and the read-only line with its link to the web board.
export function BoardHeader({
  projectKey,
  featureKey,
  header,
  figures,
  item,
  row,
  overviewSettled,
}: BoardHeaderProps) {
  const theme = useTheme();
  const { colors } = theme;
  const verified = item?.statusVerified ?? null;
  const stale = verified?.stale === true;
  const percent = pointsPercent(header);

  const figure = (testID: string, value: string, label: string) => (
    <View
      style={{
        flex: 1,
        backgroundColor: colors.surface2,
        borderRadius: theme.radius.md,
        paddingVertical: theme.space[2],
        paddingHorizontal: theme.space[3],
      }}
    >
      <Text
        testID={testID}
        style={{
          color: colors.text,
          fontSize: theme.font.size.md,
          fontWeight: theme.font.weight.semibold,
          fontVariant: ['tabular-nums'],
        }}
      >
        {value}
      </Text>
      <Text style={{ color: colors.muted, fontSize: theme.font.size.xs }}>{label}</Text>
    </View>
  );

  return (
    <View
      testID={featureBoardIds.header}
      style={{
        padding: theme.space[4],
        gap: theme.space[2],
        backgroundColor: colors.surface,
        borderBottomWidth: 1,
        borderBottomColor: colors.border,
      }}
    >
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: theme.space[2] }}>
        <Text
          testID={featureBoardIds.key}
          style={{ color: colors.muted, fontSize: theme.font.size.sm, fontFamily: theme.font.mono }}
        >
          {header.key}
        </Text>
        <StatePill state={header.state} />
        <HealthPill health={item?.health ?? null} />
      </View>
      <Text
        testID={featureBoardIds.title}
        style={{
          color: colors.text,
          fontSize: theme.font.size.body15,
          fontWeight: theme.font.weight.semibold,
        }}
      >
        {header.title}
      </Text>
      {item === undefined ? null : item.statusLine === '' ? (
        <Text testID={featureBoardIds.status} style={{ color: colors.muted }}>
          No status yet.
        </Text>
      ) : (
        <>
          <Text
            testID={featureBoardIds.status}
            style={{ color: colors.text, fontSize: theme.font.size.body15 }}
          >
            {item.statusLine}
          </Text>
          {verified === null ? null : (
            <View style={{ flexDirection: 'row', alignItems: 'center', gap: theme.space[1] }}>
              <Text
                testID={featureBoardIds.statusBy}
                style={{
                  fontSize: theme.font.size.xs,
                  color: stale ? colors.warn : colors.muted,
                  fontWeight: stale ? theme.font.weight.semibold : undefined,
                }}
              >
                {statusBy(verified)}
              </Text>
              {stale ? (
                <Text
                  testID={featureBoardIds.statusStale}
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
          )}
        </>
      )}
      <View style={{ flexDirection: 'row', gap: theme.space[2], marginTop: theme.space[2] }}>
        {figure(featureBoardIds.figureStories, text(figures.stories), 'Stories')}
        {figure(featureBoardIds.figureBugs, text(figures.bugs), 'Bugs')}
        {figure(featureBoardIds.figureTasks, text(figures.tasks), 'Tasks')}
        {figure(
          featureBoardIds.figurePoints,
          figures.points === null ? '—' : text(figures.points),
          'Points',
        )}
      </View>
      <View style={{ marginTop: theme.space[2] }}>
        {row !== undefined ? (
          <ProgressBar rollups={row.rollups} />
        ) : overviewSettled ? (
          // A Closed Feature is folded out of the overview: draw the web header's points percent instead.
          <View
            testID={featureBoardIds.percent}
            accessibilityRole="progressbar"
            accessibilityValue={{ min: 0, max: 100, now: percent }}
            style={{
              height: layout.progress,
              borderRadius: theme.radius.xs,
              overflow: 'hidden',
              backgroundColor: colors.surface2,
            }}
          >
            <View style={{ width: `${percent}%`, height: '100%', backgroundColor: colors.ok }} />
          </View>
        ) : null}
      </View>
      <View
        style={{
          flexDirection: 'row',
          alignItems: 'center',
          gap: theme.space[2],
          marginTop: theme.space[2],
        }}
      >
        <Text style={{ flex: 1, color: colors.muted, fontSize: theme.font.size.sm }}>
          Read-only. Tasks move on the web.
        </Text>
        <Pressable
          testID={featureBoardIds.webLink}
          accessibilityRole="link"
          hitSlop={layout.compactSlop}
          onPress={() =>
            void openBrowserAsync(webUrl(platform().apiOrigin(), projectKey, featureKey, 'board'))
          }
        >
          <Text
            style={{
              color: colors.accent,
              fontSize: theme.font.size.sm,
              fontWeight: theme.font.weight.medium,
            }}
          >
            Open on web
          </Text>
        </Pressable>
      </View>
    </View>
  );
}

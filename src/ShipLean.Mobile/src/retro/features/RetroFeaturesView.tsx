import type {
  RetroFeatureRowDto,
  RetroInsightsDto,
  RetroThemeInsightDto,
} from '@shiplean/core/api/retros';
import { RETRO_CATEGORIES, type RetroCategory } from '@shiplean/core/api/retros';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { Pressable, RefreshControl, ScrollView, Text, View } from 'react-native';
import { links } from '../../links';
import { useTheme } from '../../theme';
import { EmptyState } from '../../ui/EmptyState';
import { ErrorState } from '../../ui/ErrorState';
import { Skeleton } from '../../ui/Skeleton';
import { StatePill } from '../../ui/StatePill';
import { tint } from '../../ui/layout';
import { CATEGORY_LABELS } from '../notes/notesModel';
import { useRetroFeaturesQuery, useRetroInsightsQuery } from '../queries';
import {
  retroFeatureBarId,
  retroFeatureLastId,
  retroFeatureNoRetroId,
  retroFeaturePartId,
  retroFeatureRowId,
  retroFeatureToDecideId,
  retroFeaturesIds,
  retroHeatActionId,
  retroHeatCellId,
  retroHeatFixId,
  retroHeatId,
  retroHeatNoActionId,
  retroIds,
} from '../testIds';
import {
  categoryBarParts,
  closedFixColumns,
  featureRows,
  heatMax,
  heatPercent,
  recurringThemes,
  toDecide,
} from './featuresModel';

export interface RetroFeaturesViewProps {
  projectId: string;
}

const plural = (n: number, one: string, many: string) => `${n} ${n === 1 ? one : many}`;

// retro.html state `features`: recurring themes over the last six sprints as heat strips, then the Features with
// retros. Read only: a tap opens the item. Each read fails, retries and is empty on its own.
export function RetroFeaturesView({ projectId }: RetroFeaturesViewProps) {
  const theme = useTheme();
  const router = useRouter();
  const { key: projectKey } = useLocalSearchParams<{ key: string }>();
  const insights = useRetroInsightsQuery(projectId, '6');
  const features = useRetroFeaturesQuery(projectId);
  const open = (itemKey: string) => router.push(links.item(projectKey, itemKey));
  const refreshing =
    (insights.isRefetching || features.isRefetching) && !insights.isPending && !features.isPending;

  return (
    <ScrollView
      testID={retroIds.features}
      refreshControl={
        <RefreshControl
          refreshing={refreshing}
          onRefresh={() => {
            void insights.refetch();
            void features.refetch();
          }}
        />
      }
      contentContainerStyle={{ paddingBottom: theme.space[4] }}
    >
      <SectionHeading
        title="Recurring themes"
        count={recurringCount(insights.data)}
        countId={retroFeaturesIds.recurringCount}
      />
      <View testID={retroFeaturesIds.recurring}>
        {insights.error !== null ? (
          <View testID={retroFeaturesIds.recurringError}>
            <ErrorState
              title="Couldn't load the recurring themes."
              action={{
                label: 'Retry',
                busy: insights.isFetching,
                onPress: () => void insights.refetch(),
              }}
            />
          </View>
        ) : insights.data === undefined ? (
          <Skeleton rows={3} />
        ) : (
          <RecurringList insights={insights.data} onOpen={open} />
        )}
      </View>
      <SectionHeading title="Features with retros" />
      <View testID={retroFeaturesIds.list}>
        {features.error !== null ? (
          <View testID={retroFeaturesIds.listError}>
            <ErrorState
              title="Couldn't load the Features with retros."
              action={{
                label: 'Retry',
                busy: features.isFetching,
                onPress: () => void features.refetch(),
              }}
            />
          </View>
        ) : features.data === undefined ? (
          <Skeleton rows={4} />
        ) : (
          <FeatureList rows={featureRows(features.data)} onOpen={open} />
        )}
      </View>
    </ScrollView>
  );
}

const recurringCount = (insights: RetroInsightsDto | undefined) =>
  insights === undefined ? undefined : recurringThemes(insights).length;

function SectionHeading({
  title,
  count,
  countId,
}: {
  title: string;
  count?: number;
  countId?: string;
}) {
  const theme = useTheme();
  return (
    <View
      accessibilityRole="header"
      style={{
        flexDirection: 'row',
        alignItems: 'baseline',
        gap: theme.space[2],
        paddingHorizontal: theme.space[4],
        paddingTop: theme.space[4],
        paddingBottom: theme.space[2],
      }}
    >
      <Text
        style={{
          fontSize: theme.font.size.sm,
          fontWeight: theme.font.weight.semibold,
          color: theme.colors.muted,
        }}
      >
        {title}
      </Text>
      {count === undefined ? null : (
        <Text testID={countId} style={{ fontSize: theme.font.size.sm, color: theme.colors.muted }}>
          {count}
        </Text>
      )}
    </View>
  );
}

function RecurringList({
  insights,
  onOpen,
}: {
  insights: RetroInsightsDto;
  onOpen: (key: string) => void;
}) {
  const themes = recurringThemes(insights);
  if (themes.length === 0) {
    return (
      <View testID={retroFeaturesIds.recurringEmpty}>
        <EmptyState title="No recurring themes in the last six sprints." />
      </View>
    );
  }
  // The shading is against the largest cell over the recurring themes shown.
  const max = heatMax(themes);
  return (
    <>
      {themes.map((row) => (
        <HeatCard key={row.theme.id} insights={insights} row={row} max={max} onOpen={onOpen} />
      ))}
    </>
  );
}

// retro.html .heat-card: the name, one cell per sprint shaded by count, the first and last column labels, then the
// action items.
function HeatCard({
  insights,
  row,
  max,
  onOpen,
}: {
  insights: RetroInsightsDto;
  row: RetroThemeInsightDto;
  max: number;
  onOpen: (key: string) => void;
}) {
  const theme = useTheme();
  const { colors } = theme;
  const { columns } = insights;
  const fixes = closedFixColumns(insights, row.theme.id);
  const label = { fontSize: theme.font.size.xs, color: colors.muted } as const;
  return (
    <View
      testID={retroHeatId(row.theme.id)}
      style={{
        marginHorizontal: theme.space[4],
        marginBottom: theme.space[2],
        padding: theme.space[3],
        gap: theme.space[2],
        borderRadius: theme.radius.md,
        borderWidth: 1,
        borderColor: colors.border,
        backgroundColor: colors.surface,
      }}
    >
      <Text
        numberOfLines={1}
        style={{
          fontSize: theme.font.size.base,
          fontWeight: theme.font.weight.semibold,
          color: colors.text,
        }}
      >
        {row.theme.name}
      </Text>
      <View style={{ flexDirection: 'row', gap: 4 }}>
        {columns.map((column, i) => {
          const count = row.counts[i] ?? 0;
          return (
            <View
              key={column.key}
              testID={retroHeatCellId(row.theme.id, i)}
              accessibilityLabel={`${column.label}: ${plural(count, 'mention', 'mentions')}`}
              style={{
                flex: 1,
                height: 28,
                borderRadius: 5,
                alignItems: 'center',
                justifyContent: 'center',
                backgroundColor:
                  count > 0 ? tint(colors.danger, heatPercent(count, max) / 100) : colors.surface2,
              }}
            >
              {count > 0 ? (
                <Text
                  style={{
                    fontSize: theme.font.size.xs,
                    fontWeight: theme.font.weight.semibold,
                    color: colors.text,
                  }}
                >
                  {count}
                </Text>
              ) : null}
              {fixes.includes(i) ? (
                <View
                  testID={retroHeatFixId(row.theme.id, i)}
                  accessibilityLabel="A fix closed here"
                  style={{
                    position: 'absolute',
                    top: -2,
                    bottom: -2,
                    right: -3,
                    borderRightWidth: 2,
                    borderStyle: 'dashed',
                    borderColor: colors.ok,
                  }}
                />
              ) : null}
            </View>
          );
        })}
      </View>
      <View style={{ flexDirection: 'row', justifyContent: 'space-between' }}>
        <Text style={label}>{columns[0]?.label}</Text>
        <Text style={label}>{columns.length > 1 ? columns[columns.length - 1].label : ''}</Text>
      </View>
      <View
        style={{
          flexDirection: 'row',
          flexWrap: 'wrap',
          alignItems: 'center',
          gap: theme.space[2],
        }}
      >
        {row.actionItems.length === 0 ? (
          <Text testID={retroHeatNoActionId(row.theme.id)} style={label}>
            No action item yet
          </Text>
        ) : (
          row.actionItems.map((action) => (
            <Pressable
              key={action.workItemId}
              testID={retroHeatActionId(row.theme.id, action.key)}
              accessibilityRole="link"
              hitSlop={{ top: 12, bottom: 12 }}
              onPress={() => onOpen(action.key)}
              style={{ flexDirection: 'row', alignItems: 'center', gap: theme.space[2] }}
            >
              <Text
                style={{
                  fontFamily: theme.font.mono,
                  fontSize: theme.font.size.sm,
                  color: colors.text,
                }}
              >
                {action.key}
              </Text>
              <StatePill state={action.state} />
            </Pressable>
          ))
        )}
        <Text style={label}>{plural(row.total, 'mention', 'mentions')}</Text>
      </View>
    </View>
  );
}

function FeatureList({
  rows,
  onOpen,
}: {
  rows: RetroFeatureRowDto[];
  onOpen: (key: string) => void;
}) {
  if (rows.length === 0) {
    return (
      <View testID={retroFeaturesIds.listEmpty}>
        <EmptyState title="No Features have retros yet." />
      </View>
    );
  }
  return (
    <>
      <Legend />
      {rows.map((row) => (
        <FeatureRow key={row.item.id} row={row} onOpen={onOpen} />
      ))}
    </>
  );
}

function useCategoryColours(): Record<RetroCategory, string> {
  const { colors } = useTheme();
  return {
    wentWell: colors.ok,
    wentBadly: colors.danger,
    improve: colors.accent,
    notInPlan: colors.purple,
  };
}

// retro.html .legend: the bar's four colours.
function Legend() {
  const theme = useTheme();
  const colour = useCategoryColours();
  return (
    <View
      testID={retroFeaturesIds.legend}
      style={{
        flexDirection: 'row',
        flexWrap: 'wrap',
        columnGap: theme.space[4],
        rowGap: theme.space[1],
        paddingHorizontal: theme.space[4],
        paddingBottom: theme.space[2],
      }}
    >
      {RETRO_CATEGORIES.map((category) => (
        <View key={category} style={{ flexDirection: 'row', alignItems: 'center', gap: 4 }}>
          <View
            style={{ width: 8, height: 8, borderRadius: 2, backgroundColor: colour[category] }}
          />
          <Text style={{ fontSize: theme.font.size.xs, color: theme.colors.muted }}>
            {CATEGORY_LABELS[category]}
          </Text>
        </View>
      ))}
    </View>
  );
}

// retro.html .frr: type, key, state, when; the title; the category bar and counts; what is left to decide.
function FeatureRow({ row, onOpen }: { row: RetroFeatureRowDto; onOpen: (key: string) => void }) {
  const theme = useTheme();
  const { colors } = theme;
  const colour = useCategoryColours();
  const { key } = row.item;
  const small = { fontSize: theme.font.size.sm, color: colors.muted } as const;
  const parts = categoryBarParts(row.totals.counts);
  return (
    <Pressable
      testID={retroFeatureRowId(key)}
      accessibilityRole="link"
      onPress={() => onOpen(key)}
      style={{
        minHeight: 44,
        gap: theme.space[1],
        paddingHorizontal: theme.space[4],
        paddingVertical: theme.space[3],
        borderTopWidth: 1,
        borderColor: colors.border,
      }}
    >
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: theme.space[2] }}>
        <Text style={small}>{row.item.type.name}</Text>
        <Text style={{ ...small, fontFamily: theme.font.mono }}>{key}</Text>
        <StatePill state={row.state} />
        <View style={{ flex: 1 }} />
        {row.lastRetroLabel === null ? null : (
          <Text testID={retroFeatureLastId(key)} style={small}>
            {row.lastRetroLabel}
          </Text>
        )}
      </View>
      <Text
        style={{
          fontSize: theme.font.size.base,
          fontWeight: theme.font.weight.semibold,
          color: colors.text,
        }}
      >
        {row.item.title}
      </Text>
      {row.noFeatureRetro ? (
        <View
          testID={retroFeatureNoRetroId(key)}
          style={{
            alignSelf: 'flex-start',
            paddingHorizontal: 7,
            paddingVertical: 2,
            borderRadius: 10,
            borderWidth: 1,
            borderStyle: 'dashed',
            borderColor: colors.border,
          }}
        >
          <Text style={{ fontSize: theme.font.size.xs, color: colors.muted }}>
            No Feature retro yet
          </Text>
        </View>
      ) : null}
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: theme.space[2] }}>
        <View
          testID={retroFeatureBarId(key)}
          style={{
            flexDirection: 'row',
            width: 120,
            height: 8,
            gap: 2,
            borderRadius: 4,
            overflow: 'hidden',
          }}
        >
          {parts.map((part) => (
            <View
              key={part.category}
              testID={retroFeaturePartId(key, part.category)}
              style={{ flex: part.count, backgroundColor: colour[part.category] }}
            />
          ))}
        </View>
        <Text style={small}>
          {RETRO_CATEGORIES.map((category) => row.totals.counts[category]).join(' · ')}
        </Text>
        <View style={{ flex: 1 }} />
        {toDecide(row) ? (
          <Text
            testID={retroFeatureToDecideId(key)}
            style={{
              fontSize: theme.font.size.sm,
              fontWeight: theme.font.weight.semibold,
              color: colors.accent,
            }}
          >
            {row.totals.toDecide} to decide
          </Text>
        ) : (
          <Text style={small}>nothing to decide</Text>
        )}
      </View>
      <Text style={small}>
        {plural(row.retros, 'retro', 'retros')} ·{' '}
        {plural(row.totals.actions, 'action item', 'action items')}
      </Text>
    </Pressable>
  );
}

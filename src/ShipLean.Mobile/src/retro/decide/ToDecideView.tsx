import type { RetroMergeProposalDto, RetroPointDto } from '@shiplean/core/api/retros';
import { retroKeys } from '@shiplean/core/api/retrosApi';
import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { Pressable, RefreshControl, ScrollView, Text, View } from 'react-native';
import { usePending } from '../../actions/ActionRunnerProvider';
import { useTheme } from '../../theme';
import { Button } from '../../ui/Button';
import { EmptyState } from '../../ui/EmptyState';
import { ErrorState } from '../../ui/ErrorState';
import { layout, tint } from '../../ui/layout';
import { Skeleton } from '../../ui/Skeleton';
import type { MergeAction } from '../actions';
import { Face } from '../notes/Face';
import { authorLabel, CATEGORY_LABELS } from '../notes/notesModel';
import { useRetroInsightsQuery, useSprintRetroQuery } from '../queries';
import { useRetroActions } from '../RetroActions';
import {
  retroAddActionId,
  retroDecideCardId,
  retroDecideCauseId,
  retroDecideLogId,
  retroDecideLogToggleId,
  retroIds,
  retroNotNeededId,
  retroPointId,
  retroPointTextId,
  retroProposalId,
  retroProposalKeepId,
  retroProposalMergeId,
} from '../testIds';
import { DecideSheet } from './DecideSheet';
import {
  decideCards,
  logEntryCount,
  logSections,
  pointCountLabel,
  pointsToDecide,
  proposalText,
  type DecideCard,
} from './toDecideModel';

export interface ToDecideViewProps {
  projectId: string;
  sprintId: string;
}

const isMerge = (kind: string) => kind === 'mergeThemes' || kind === 'keepApart';

// The sprint's undecided points by work item, Feature retros first, and the theme merges agents proposed (D10).
export function ToDecideView({ projectId, sprintId }: ToDecideViewProps) {
  const theme = useTheme();
  const { colors } = theme;
  const queryClient = useQueryClient();
  const { dispatch } = useRetroActions();
  const retro = useSprintRetroQuery(sprintId);
  const insights = useRetroInsightsQuery(projectId, '6');
  const [deciding, setDeciding] = useState<RetroPointDto | null>(null);

  // A merge or keep-apart that is queued, sent or held hides its card; a failed one shows it again.
  const answered = usePending((p) => p.action.area === 'retro' && isMerge(p.action.kind));
  const hidden = new Set(
    answered.filter((p) => p.state !== 'failed').map((p) => (p.action as MergeAction).proposalId),
  );
  // The insights read lists what is still pending; read it again when an answer lands.
  const sentIds = useRef<Set<string> | null>(null);
  useEffect(() => {
    const sent = answered.filter((p) => p.state === 'sent').map((p) => p.id);
    if (sentIds.current === null) {
      sentIds.current = new Set(sent);
      return;
    }
    const seen = sentIds.current;
    const fresh = sent.filter((id) => !seen.has(id));
    for (const id of fresh) seen.add(id);
    if (fresh.length > 0)
      void queryClient.invalidateQueries({ queryKey: retroKeys.insights(projectId, '6') });
  }, [answered, queryClient, projectId]);

  const cards = useMemo(() => decideCards(retro.data?.cards ?? []), [retro.data]);
  const proposals = (insights.data?.mergeProposals ?? []).filter((m) => !hidden.has(m.id));

  const answer = (kind: MergeAction['kind'], p: RetroMergeProposalDto) =>
    dispatch({
      area: 'retro',
      kind,
      itemId: null,
      key: null,
      projectId,
      proposalId: p.id,
      fromName: p.from.name,
      intoName: p.into.name,
    });

  const refetch = () => {
    void retro.refetch();
    void insights.refetch();
  };

  const heading = (text: string, count?: string) => (
    <View
      accessibilityRole="header"
      style={{
        flexDirection: 'row',
        alignItems: 'center',
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
          color: colors.muted,
        }}
      >
        {text}
      </Text>
      {count === undefined ? null : (
        <Text style={{ fontSize: theme.font.size.sm, color: colors.accent }}>{count}</Text>
      )}
    </View>
  );

  const proposalCard = (p: RetroMergeProposalDto) => (
    <View
      key={p.id}
      testID={retroProposalId(p.id)}
      style={{
        marginHorizontal: theme.space[4],
        marginBottom: theme.space[3],
        padding: theme.space[3],
        gap: theme.space[2],
        borderRadius: theme.radius.lg,
        borderWidth: 1,
        borderColor: colors.border,
        backgroundColor: colors.surface,
      }}
    >
      <Text
        style={{
          fontSize: theme.font.size.body15,
          fontWeight: theme.font.weight.semibold,
          color: colors.text,
        }}
      >
        {proposalText(p)}
      </Text>
      <Text style={{ fontSize: theme.font.size.sm, color: colors.muted }}>
        {p.reason} · {pointCountLabel(p.pointCount)}
      </Text>
      <View style={{ flexDirection: 'row', gap: theme.space[2] }}>
        <View style={{ flex: 1 }}>
          <Button
            label="Merge"
            onPress={() => answer('mergeThemes', p)}
            testID={retroProposalMergeId(p.id)}
          />
        </View>
        <View style={{ flex: 1 }}>
          <Button
            label="Keep apart"
            variant="ghost"
            onPress={() => answer('keepApart', p)}
            testID={retroProposalKeepId(p.id)}
          />
        </View>
      </View>
    </View>
  );

  const pointRow = (point: RetroPointDto) => {
    const compact = (label: string, onPress: () => void, testID: string, solid: boolean) => (
      <Pressable
        testID={testID}
        accessibilityRole="button"
        hitSlop={{ top: layout.compactSlop, bottom: layout.compactSlop }}
        onPress={onPress}
        style={{
          height: layout.compact,
          paddingHorizontal: theme.space[3],
          borderRadius: theme.radius.md,
          borderWidth: 1,
          borderColor: solid ? colors.accent : colors.border,
          backgroundColor: solid ? colors.accent : 'transparent',
          alignItems: 'center',
          justifyContent: 'center',
        }}
      >
        <Text
          style={{
            fontSize: theme.font.size.base,
            fontWeight: theme.font.weight.semibold,
            color: solid ? colors.accentFg : colors.muted,
          }}
        >
          {label}
        </Text>
      </Pressable>
    );
    return (
      <View
        key={point.id}
        testID={retroPointId(point.id)}
        style={{
          gap: theme.space[2],
          paddingHorizontal: theme.space[4],
          paddingVertical: theme.space[3],
          borderBottomWidth: 1,
          borderBottomColor: colors.border,
        }}
      >
        <Text style={{ fontSize: theme.font.size.sm, color: colors.muted }}>
          {CATEGORY_LABELS[point.category]}
        </Text>
        <Text
          testID={retroPointTextId(point.id)}
          style={{ fontSize: theme.font.size.body15, lineHeight: 21, color: colors.text }}
        >
          {point.text}
        </Text>
        {point.rootCause === null ? null : (
          <Text
            testID={retroDecideCauseId(point.id)}
            style={{ fontSize: theme.font.size.sm, color: colors.muted }}
          >
            <Text style={{ fontWeight: theme.font.weight.semibold }}>Root cause: </Text>
            {point.rootCause}
          </Text>
        )}
        <View style={{ flexDirection: 'row', gap: theme.space[2] }}>
          {compact('+ Action item', () => setDeciding(point), retroAddActionId(point.id), true)}
          {compact('Not needed', () => setDeciding(point), retroNotNeededId(point.id), false)}
        </View>
      </View>
    );
  };

  return (
    <>
      <ScrollView
        testID={retroIds.toDecide}
        refreshControl={
          <RefreshControl
            refreshing={(retro.isRefetching || insights.isRefetching) && !retro.isPending}
            onRefresh={refetch}
          />
        }
      >
        {insights.error !== null ? (
          <View
            testID={retroIds.toDecideInsightsError}
            accessibilityRole="alert"
            style={{ padding: theme.space[4], gap: theme.space[2] }}
          >
            <Text style={{ color: colors.danger, fontSize: theme.font.size.md }}>
              Couldn&apos;t load the proposed theme merges.
            </Text>
            <Button
              label="Retry"
              variant="ghost"
              busy={insights.isFetching}
              onPress={() => void insights.refetch()}
              testID={retroIds.toDecideInsightsRetry}
            />
          </View>
        ) : null}
        {proposals.length === 0 ? null : (
          <>
            {heading('Theme merge proposed by an agent')}
            {proposals.map(proposalCard)}
          </>
        )}
        {retro.error !== null ? (
          <ErrorState
            title="Couldn't load what is left to decide."
            action={{ label: 'Retry', busy: retro.isFetching, onPress: () => void retro.refetch() }}
          />
        ) : retro.data === undefined ? (
          <Skeleton rows={6} />
        ) : null}
        {cards.length === 0 ? null : (
          <>
            {heading('Feature retros and items', `${pointsToDecide(cards)} to decide`)}
            {cards.map((card) => (
              <DecideCardView key={card.id} card={card} renderPoint={pointRow} />
            ))}
          </>
        )}
        {retro.data !== undefined &&
        cards.length === 0 &&
        proposals.length === 0 &&
        insights.error === null ? (
          insights.isPending ? (
            <Skeleton rows={2} />
          ) : (
            <View testID={retroIds.toDecideEmpty}>
              <EmptyState title="Nothing to decide." />
            </View>
          )
        ) : null}
      </ScrollView>
      {deciding === null ? null : (
        <DecideSheet
          point={deciding}
          projectId={projectId}
          open
          onClose={() => setDeciding(null)}
        />
      )}
    </>
  );
}

interface DecideCardViewProps {
  card: DecideCard;
  renderPoint: (point: RetroPointDto) => ReactNode;
}

// One retro card (retro.html .fretro-h and .fretro-sub): the item, who logged it, its undecided points and the raw
// failure log, folded until tapped.
function DecideCardView({ card, renderPoint }: DecideCardViewProps) {
  const theme = useTheme();
  const { colors } = theme;
  const [open, setOpen] = useState(false);
  const author = authorLabel(card.author);
  const sections = logSections(card.failureLog);
  return (
    <View testID={retroDecideCardId(card.id)}>
      <View
        style={{
          gap: theme.space[1],
          paddingHorizontal: theme.space[4],
          paddingTop: theme.space[3],
          paddingBottom: theme.space[2],
          backgroundColor: colors.surface,
          borderTopWidth: 1,
          borderTopColor: colors.border,
        }}
      >
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: theme.space[2] }}>
          {card.workItem === null ? null : (
            <Text
              style={{
                fontFamily: theme.font.mono,
                fontSize: theme.font.size.sm,
                color: colors.text,
              }}
            >
              {card.workItem.key}
            </Text>
          )}
          <Text
            numberOfLines={1}
            style={{
              flex: 1,
              fontSize: theme.font.size.body15,
              fontWeight: theme.font.weight.semibold,
              color: colors.text,
            }}
          >
            {card.workItem?.title ?? 'This sprint'}
          </Text>
          <Text
            style={{
              fontSize: theme.font.size.xs,
              fontWeight: theme.font.weight.semibold,
              color: colors.text,
            }}
          >
            {card.points.length} to decide
          </Text>
        </View>
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: theme.space[2] }}>
          <Face user={card.author.user} size={20} />
          <Text style={{ fontSize: theme.font.size.sm, color: colors.muted }}>
            Retro by {author.name}
          </Text>
          {author.lane === null ? null : (
            <View
              style={{
                paddingHorizontal: theme.space[2],
                borderRadius: theme.radius.xs,
                backgroundColor: tint(colors.purple, 0.14),
              }}
            >
              <Text style={{ fontSize: theme.font.size.xs, color: colors.text }}>
                {author.lane}
              </Text>
            </View>
          )}
          <Text style={{ fontSize: theme.font.size.sm, color: colors.muted }}>
            · {card.createdAtLabel}
          </Text>
        </View>
      </View>
      {card.points.map(renderPoint)}
      {sections.length === 0 ? null : (
        <>
          <Pressable
            testID={retroDecideLogToggleId(card.id)}
            accessibilityRole="button"
            accessibilityState={{ expanded: open }}
            onPress={() => setOpen(!open)}
            style={{
              minHeight: theme.touch.row,
              justifyContent: 'center',
              paddingHorizontal: theme.space[4],
              backgroundColor: colors.surface,
              borderBottomWidth: 1,
              borderBottomColor: colors.border,
            }}
          >
            <Text style={{ fontSize: theme.font.size.sm, color: colors.muted }}>
              {open ? '▾' : '▸'} Raw failure log · {logEntryCount(card.failureLog)}{' '}
              {logEntryCount(card.failureLog) === 1 ? 'entry' : 'entries'}
            </Text>
          </Pressable>
          {open ? (
            <View
              testID={retroDecideLogId(card.id)}
              style={{
                gap: theme.space[2],
                paddingHorizontal: theme.space[4],
                paddingVertical: theme.space[3],
                backgroundColor: colors.surface2,
              }}
            >
              {sections.map((s) => (
                <View key={s.label} style={{ gap: 2 }}>
                  <Text
                    style={{
                      fontSize: theme.font.size.sm,
                      fontWeight: theme.font.weight.semibold,
                      color: colors.text,
                    }}
                  >
                    {s.label}
                  </Text>
                  {s.lines.map((line, i) => (
                    <Text key={i} style={{ fontSize: theme.font.size.sm, color: colors.muted }}>
                      {line}
                    </Text>
                  ))}
                </View>
              ))}
            </View>
          ) : null}
        </>
      )}
    </View>
  );
}

import type { BacklogRowDto } from '@shiplean/core/api/views';
import { useMemo, useState } from 'react';
import { Pressable, ScrollView, Text, View } from 'react-native';
import { rankPlan, type RankPlan, type RankTarget } from '../../actions/rankPlan';
import { useTheme } from '../../theme';
import { BottomSheet } from '../../ui/BottomSheet';
import { Button } from '../../ui/Button';
import { Segmented } from '../../ui/Segmented';
import type { BacklogLevel, BacklogMove } from '../actions';
import { useBacklogActions } from '../BacklogActions';
import {
  moveApplyId,
  moveBackId,
  moveFilteredId,
  moveItemId,
  moveOptionId,
  moveOptionToId,
  movePickId,
  movePreviewId,
  movePreviewPosId,
  movePreviewRowId,
  moveSheetId,
  moveSideId,
  moveWarningId,
  type MoveChoice,
} from '../testIds';
import { blockedWarning, plannedKeys } from './blockedBy';
import { rankRows } from './rankRows';

export interface MoveSheetProps {
  projectId: string;
  row: BacklogRowDto;
  // The level's rows in rank order.
  rows: BacklogRowDto[];
  level: BacklogLevel;
  // A criteria filter (Epic or Feature) is on: the web turns reordering off, so the sheet offers no move.
  filtering?: boolean;
  open: boolean;
  onClose: () => void;
}

const LEVEL_NAMES: Record<BacklogLevel, string> = {
  epics: 'Epics',
  features: 'Features',
  stories: 'Stories',
};

const CHOICES: { choice: MoveChoice; label: string; icon: string; target: RankTarget | null }[] = [
  { choice: 'top', label: 'Top', icon: '↑', target: { to: 'top' } },
  { choice: 'bottom', label: 'Bottom', icon: '↓', target: { to: 'bottom' } },
  { choice: 'up', label: 'Up one', icon: '↑', target: { to: 'up', by: 1 } },
  { choice: 'down', label: 'Down one', icon: '↓', target: { to: 'down', by: 1 } },
];

type Step = { at: 'choose' } | { at: 'pick' } | { at: 'preview'; plan: RankPlan; from: Step };

// The Move sheet (design backlog.html, move and preview): Top, Bottom, Up one, Down one or Above or below another
// item. A choice shows the rank preview; Apply dispatches one move through the runner.
export function MoveSheet({
  projectId,
  row,
  rows,
  level,
  filtering,
  open,
  onClose,
}: MoveSheetProps) {
  const theme = useTheme();
  const { colors } = theme;
  const [step, setStep] = useState<Step>({ at: 'choose' });
  const [side, setSide] = useState<'above' | 'below'>('above');
  const ranked = useMemo(() => rankRows(rows), [rows]);
  const index = ranked.findIndex((r) => r.id === row.id);

  const plan = (target: RankTarget) => rankPlan(ranked, row.id, target);
  const preview = (target: RankTarget) => {
    const next = plan(target);
    if (next !== null) setStep({ at: 'preview', plan: next, from: step });
  };
  // Why a choice that changes nothing is off.
  const offReason = (choice: MoveChoice): string =>
    index < 0
      ? 'Not in this list'
      : choice === 'top' || choice === 'up'
        ? 'Already at the top'
        : 'Already at the bottom';

  const subtitle =
    index < 0
      ? row.key
      : `${row.key} · rank ${ranked[index].rank} of ${ranked.length} ${LEVEL_NAMES[level]}`;
  const muted = { color: colors.muted, fontSize: theme.font.size.sm };

  const header = (
    <View style={{ marginBottom: theme.space[3] }}>
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
      <Text testID={moveItemId} style={muted}>
        {subtitle}
      </Text>
    </View>
  );

  const option = (
    choice: MoveChoice,
    label: string,
    icon: string,
    target: RankTarget | null,
    wide: boolean,
  ) => {
    const result = target === null ? null : plan(target);
    const off = target !== null && result === null;
    return (
      <Pressable
        key={choice}
        testID={moveOptionId(choice)}
        accessibilityRole="button"
        accessibilityLabel={label}
        accessibilityState={{ disabled: off }}
        disabled={off}
        onPress={() => (target === null ? setStep({ at: 'pick' }) : preview(target))}
        style={{
          width: wide ? '100%' : '48.5%',
          minHeight: 56,
          flexDirection: 'row',
          alignItems: 'center',
          gap: theme.space[2],
          paddingHorizontal: theme.space[3],
          borderRadius: theme.radius.md,
          backgroundColor: colors.surface2,
          opacity: off ? 0.5 : 1,
        }}
      >
        <Text style={{ color: colors.text, fontSize: theme.font.size.md }}>{icon}</Text>
        <Text
          style={{
            color: colors.text,
            fontSize: theme.font.size.body15,
            fontWeight: theme.font.weight.semibold,
          }}
        >
          {label}
        </Text>
        {target === null ? null : (
          <Text testID={moveOptionToId(choice)} style={{ ...muted, marginLeft: 'auto' }}>
            {result === null ? offReason(choice) : `to ${result.toRank}`}
          </Text>
        )}
      </Pressable>
    );
  };

  const choose = (
    <>
      {header}
      <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: theme.space[2] }}>
        {CHOICES.map((c) => option(c.choice, c.label, c.icon, c.target, false))}
        {option('other', 'Above or below another item…', '⌕', null, true)}
      </View>
    </>
  );

  const pick = (
    <>
      {header}
      <Segmented
        options={[
          { value: 'above', label: 'Above' },
          { value: 'below', label: 'Below' },
        ]}
        value={side}
        onChange={setSide}
        testID={moveSideId}
      />
      <ScrollView style={{ maxHeight: 360, marginTop: theme.space[2] }}>
        {ranked
          .filter((r) => r.id !== row.id)
          .map((other) => {
            const off = plan({ to: side, otherId: other.id }) === null;
            return (
              <Pressable
                key={other.id}
                testID={movePickId(other.key)}
                accessibilityRole="button"
                accessibilityState={{ disabled: off }}
                disabled={off}
                onPress={() => preview({ to: side, otherId: other.id })}
                style={{
                  minHeight: theme.touch.row,
                  justifyContent: 'center',
                  borderBottomWidth: 1,
                  borderBottomColor: colors.border,
                  opacity: off ? 0.5 : 1,
                }}
              >
                <Text
                  numberOfLines={2}
                  style={{ color: colors.text, fontSize: theme.font.size.md }}
                >
                  {other.rank} · {other.key} {other.row.title}
                </Text>
              </Pressable>
            );
          })}
      </ScrollView>
    </>
  );

  const back = () => setStep(step.at === 'preview' ? step.from : { at: 'choose' });
  const body =
    filtering === true ? (
      <Text
        testID={moveFilteredId}
        style={{ color: colors.text, fontSize: theme.font.size.body15 }}
      >
        Clear the filter to move items.
      </Text>
    ) : step.at === 'choose' ? (
      choose
    ) : step.at === 'pick' ? (
      pick
    ) : (
      <PreviewStep
        projectId={projectId}
        row={row}
        rows={rows}
        ranked={ranked}
        level={level}
        plan={step.plan}
        onBack={back}
        onApplied={onClose}
      />
    );

  return (
    <BottomSheet title="Move" open={open} onClose={onClose} testID={moveSheetId}>
      {body}
      {step.at === 'pick' && filtering !== true ? (
        <Button label="Back" variant="ghost" onPress={back} testID={moveBackId} />
      ) : null}
    </BottomSheet>
  );
}

interface PreviewStepProps {
  projectId: string;
  row: BacklogRowDto;
  rows: BacklogRowDto[];
  ranked: ReturnType<typeof rankRows>;
  level: BacklogLevel;
  plan: RankPlan;
  onBack: () => void;
  onApplied: () => void;
}

// Every row that shifts, before → after, the moved row first; the D3 warning when it would sit above its blocker.
function PreviewStep({
  projectId,
  row,
  rows,
  ranked,
  level,
  plan,
  onBack,
  onApplied,
}: PreviewStepProps) {
  const theme = useTheme();
  const { colors } = theme;
  const { dispatch } = useBacklogActions();
  const warning = blockedWarning(row.key, row.blockedBy, plannedKeys(ranked, row.id, plan.toRank));
  const action: BacklogMove = {
    area: 'backlog',
    kind: 'move',
    projectId,
    itemId: row.id,
    key: row.key,
    revision: row.revision,
    level,
    fromRank: plan.fromRank,
    toRank: plan.toRank,
    placement: plan.placement,
    undoPlacement: plan.undoPlacement,
    shifted: plan.shifted,
    warning,
  };
  const lines = [
    { key: row.key, title: row.title, from: plan.fromRank, to: plan.toRank, me: true },
    ...plan.shifted.map((s) => ({
      key: s.key,
      title: rows.find((r) => r.key === s.key)?.title ?? '',
      from: s.from,
      to: s.to,
      me: false,
    })),
  ];
  return (
    <View testID={movePreviewId} style={{ gap: theme.space[3] }}>
      <Text
        style={{
          color: colors.text,
          fontSize: theme.font.size.body15,
          fontWeight: theme.font.weight.semibold,
        }}
      >
        {`Move ${row.key} to ${plan.toRank}`}
      </Text>
      <View>
        {lines.map((line) => (
          <View
            key={line.key}
            testID={movePreviewRowId(line.key)}
            style={{ flexDirection: 'row', gap: theme.space[3], paddingVertical: theme.space[1] }}
          >
            <Text
              testID={movePreviewPosId(line.key)}
              style={{
                width: 64,
                color: line.me ? colors.accent : colors.muted,
                fontSize: theme.font.size.base,
                fontWeight: line.me ? theme.font.weight.bold : theme.font.weight.regular,
              }}
            >
              {line.from} → {line.to}
            </Text>
            <Text
              numberOfLines={1}
              style={{
                flex: 1,
                color: colors.text,
                fontSize: theme.font.size.base,
                fontWeight: line.me ? theme.font.weight.semibold : theme.font.weight.regular,
              }}
            >
              {line.key} {line.title}
            </Text>
          </View>
        ))}
      </View>
      {warning === null ? null : (
        <Text
          testID={moveWarningId}
          style={{
            color: colors.text,
            fontSize: theme.font.size.base,
            borderTopWidth: 1,
            borderTopColor: colors.border,
            paddingTop: theme.space[2],
          }}
        >
          {`⚠ ${warning}`}
        </Text>
      )}
      <View style={{ flexDirection: 'row', gap: theme.space[2] }}>
        <View style={{ flex: 1 }}>
          <Button label="Back" variant="ghost" onPress={onBack} testID={moveBackId} />
        </View>
        <View style={{ flex: 1 }}>
          <Button
            label="Apply"
            onPress={() => {
              dispatch(action);
              onApplied();
            }}
            testID={moveApplyId}
          />
        </View>
      </View>
    </View>
  );
}

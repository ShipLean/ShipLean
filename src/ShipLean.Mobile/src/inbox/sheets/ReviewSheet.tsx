import type { InboxDecisionDto, InboxItemDto } from '@shiplean/core/api/inbox';
import { useRef, useState, type ComponentRef } from 'react';
import {
  KeyboardAvoidingView,
  Platform,
  Pressable,
  ScrollView,
  Text,
  TextInput,
  View,
  useWindowDimensions,
} from 'react-native';
import { useTheme } from '../../theme';
import { BottomSheet } from '../../ui/BottomSheet';
import { Button } from '../../ui/Button';
import { useInboxActions } from '../InboxActions';
import {
  reviewChangeId,
  reviewChoiceId,
  reviewDecisionId,
  inboxIds,
  reviewOwnId,
  reviewWhyId,
} from '../testIds';
import {
  RECOMMENDED,
  canConfirm,
  decisionLabels,
  choose,
  initialReview,
  isChanged,
  openDecisions,
  reviewAction,
  reviewLead,
  reviewNote,
  reviewTitle,
  setNote,
  type ReviewChoice,
  type ReviewState,
} from './reviewModel';

export interface ReviewSheetProps {
  item: InboxItemDto;
  // Approve all opens the same sheet scrolled to its Confirm button.
  scrollToConfirm: boolean;
  projectId: string;
  onClose: () => void;
}

function Radio({
  testID,
  label,
  caption,
  on,
  onPress,
}: {
  testID: string;
  label: string;
  caption?: string;
  on: boolean;
  onPress: () => void;
}) {
  const theme = useTheme();
  const { colors } = theme;
  return (
    <Pressable
      testID={testID}
      accessibilityRole="radio"
      accessibilityState={{ selected: on }}
      onPress={onPress}
      style={{
        minHeight: theme.touch.control,
        flexDirection: 'row',
        alignItems: 'center',
        gap: theme.space[3],
        paddingVertical: theme.space[1],
      }}
    >
      <View
        style={{
          width: 20,
          height: 20,
          borderRadius: 10,
          borderWidth: 2,
          borderColor: on ? colors.accent : colors.border,
          alignItems: 'center',
          justifyContent: 'center',
        }}
      >
        {on ? (
          <View
            style={{ width: 10, height: 10, borderRadius: 5, backgroundColor: colors.accent }}
          />
        ) : null}
      </View>
      <View style={{ flex: 1 }}>
        {caption === undefined ? null : (
          <Text style={{ color: colors.muted, fontSize: theme.font.size.sm }}>{caption}</Text>
        )}
        <Text style={{ color: colors.text, fontSize: theme.font.size.body15 }}>{label}</Text>
      </View>
    </Pressable>
  );
}

function Decision({
  decision,
  id,
  state,
  editing,
  onEdit,
  onChoose,
  onNote,
}: {
  decision: InboxDecisionDto;
  id: string;
  state: ReviewState;
  editing: boolean;
  onEdit: () => void;
  onChoose: (choice: ReviewChoice) => void;
  onNote: (note: string) => void;
}) {
  const theme = useTheme();
  const { colors } = theme;
  const n = decision.number;
  const s = state.get(n) ?? RECOMMENDED;
  const changed = isChanged(s);
  const input = {
    minHeight: theme.touch.control,
    paddingHorizontal: theme.space[3],
    paddingVertical: theme.space[2],
    borderRadius: theme.radius.md,
    borderWidth: 1,
    borderColor: colors.border,
    backgroundColor: colors.surface,
    color: colors.text,
    fontSize: theme.font.size.input,
  } as const;
  return (
    <View
      testID={reviewDecisionId(n)}
      style={{
        padding: theme.space[3],
        gap: theme.space[2],
        borderBottomWidth: 1,
        borderBottomColor: colors.border,
      }}
    >
      <View style={{ flexDirection: 'row', alignItems: 'baseline', gap: theme.space[2] }}>
        <Text style={{ color: colors.muted, fontSize: theme.font.size.sm }}>{id}</Text>
        <Text style={{ flex: 1, color: colors.text, fontSize: theme.font.size.body15 }}>
          {decision.question}
        </Text>
        {changed ? (
          <Text style={{ color: colors.accent, fontSize: theme.font.size.sm }}>Changed</Text>
        ) : null}
      </View>
      {editing ? (
        <View>
          <Radio
            testID={reviewChoiceId(n, 'recommended')}
            caption="Recommended"
            label={decision.recommended}
            on={s.choice.kind === 'recommended'}
            onPress={() => onChoose({ kind: 'recommended' })}
          />
          {decision.alternatives.map((alt, index) => (
            <Radio
              key={index}
              testID={reviewChoiceId(n, `alt-${index}`)}
              caption="Alternative"
              label={alt}
              on={s.choice.kind === 'alternative' && s.choice.index === index}
              onPress={() => onChoose({ kind: 'alternative', index })}
            />
          ))}
          <Radio
            testID={reviewChoiceId(n, 'own')}
            label="Your own answer"
            on={s.choice.kind === 'own'}
            onPress={() =>
              onChoose({ kind: 'own', text: s.choice.kind === 'own' ? s.choice.text : '' })
            }
          />
        </View>
      ) : (
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: theme.space[2] }}>
          <Text style={{ flex: 1, color: colors.text, fontSize: theme.font.size.body15 }}>
            {s.choice.kind === 'alternative'
              ? (decision.alternatives[s.choice.index] ?? '')
              : s.choice.kind === 'own'
                ? s.choice.text
                : decision.recommended}
          </Text>
          <Pressable
            testID={reviewChangeId(n)}
            accessibilityRole="button"
            accessibilityLabel={`Change ${id}`}
            onPress={onEdit}
            style={{
              minHeight: theme.touch.control,
              minWidth: theme.touch.control,
              justifyContent: 'center',
              alignItems: 'center',
            }}
          >
            <Text style={{ color: colors.accent, fontSize: theme.font.size.body15 }}>Change</Text>
          </Pressable>
        </View>
      )}
      {s.choice.kind === 'own' ? (
        <TextInput
          testID={reviewOwnId(n)}
          accessibilityLabel={`Your own answer for ${id}`}
          multiline
          value={s.choice.text}
          onChangeText={(text) => onChoose({ kind: 'own', text })}
          placeholder="Type an answer"
          placeholderTextColor={colors.muted}
          style={input}
        />
      ) : null}
      {changed ? (
        <TextInput
          testID={reviewWhyId(n)}
          accessibilityLabel={`Add why for ${id}`}
          multiline
          value={s.note}
          onChangeText={onNote}
          placeholder="Add why (optional)"
          placeholderTextColor={colors.muted}
          style={input}
        />
      ) : null}
    </View>
  );
}

// Every open decision on the item with its recommended answer chosen. Change offers the alternatives and an own answer,
// Add why takes a typed reason. Confirm N dispatches one review action; nothing is sent before it.
export function ReviewSheet({ item, scrollToConfirm, projectId, onClose }: ReviewSheetProps) {
  const theme = useTheme();
  const { colors } = theme;
  const actions = useInboxActions();
  const { height } = useWindowDimensions();
  const decisions = openDecisions(item);
  const labels = decisionLabels(item);
  const [state, setState] = useState<ReviewState>(() => initialReview(decisions));
  const [editing, setEditing] = useState<ReadonlySet<number>>(new Set());
  const scroll = useRef<ComponentRef<typeof ScrollView>>(null);
  const scrolled = useRef(false);
  const off = decisions.length === 0 || !canConfirm(state);

  const confirm = () => {
    if (off) return;
    actions.dispatch(reviewAction(item, projectId, decisions, state));
    onClose();
  };

  return (
    <BottomSheet
      title={reviewTitle(item, labels)}
      open
      onClose={onClose}
      testID={inboxIds.reviewSheet}
    >
      <KeyboardAvoidingView
        testID={`${inboxIds.reviewSheet}-keyboard`}
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        style={{ gap: theme.space[3] }}
      >
        <Text
          testID={inboxIds.reviewLead}
          style={{ color: colors.muted, fontSize: theme.font.size.sm }}
        >
          {reviewLead(item)}
        </Text>
        <ScrollView
          ref={scroll}
          testID={inboxIds.reviewScroll}
          style={{ maxHeight: height * 0.55 }}
          keyboardShouldPersistTaps="handled"
          onContentSizeChange={() => {
            if (scrollToConfirm && !scrolled.current) {
              scrolled.current = true;
              scroll.current?.scrollToEnd({ animated: false });
            }
          }}
        >
          {decisions.map((d) => (
            <Decision
              key={d.number}
              decision={d}
              id={labels.get(d.number) ?? `D${d.number}`}
              state={state}
              editing={editing.has(d.number)}
              onEdit={() => setEditing((e) => new Set(e).add(d.number))}
              onChoose={(choice) => setState((s) => choose(s, d.number, choice))}
              onNote={(note) => setState((s) => setNote(s, d.number, note))}
            />
          ))}
          <Text
            testID={inboxIds.reviewNote}
            style={{
              color: colors.muted,
              fontSize: theme.font.size.body15,
              paddingVertical: theme.space[3],
            }}
          >
            {reviewNote(item, labels, state)}
          </Text>
        </ScrollView>
        <View style={{ flexDirection: 'row', gap: theme.space[2] }}>
          <View style={{ flex: 1 }}>
            <Button
              label="Cancel"
              variant="ghost"
              onPress={onClose}
              testID={inboxIds.reviewCancel}
            />
          </View>
          <View style={{ flex: 1 }}>
            <Button
              label={`Confirm ${decisions.length}`}
              onPress={confirm}
              disabled={off}
              testID={inboxIds.reviewConfirm}
            />
          </View>
        </View>
      </KeyboardAvoidingView>
    </BottomSheet>
  );
}

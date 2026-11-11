import { useState } from 'react';
import { KeyboardAvoidingView, Platform, Pressable, Text, TextInput, View } from 'react-native';
import { useTheme } from '../../theme';
import { BottomSheet } from '../../ui/BottomSheet';
import { Button } from '../../ui/Button';
import { MicSlot } from '../../voice/MicSlot';
import type { InboxAction } from '../actions';
import { useInboxActions } from '../InboxActions';
import type { AnswerSheetRequest } from '../InboxSheets';
import { answerQuickId, inboxIds } from '../testIds';

export interface AnswerSheetProps {
  request: AnswerSheetRequest;
  onClose: () => void;
}

const STATUS_MAX = 500;

type Detail<T> = T extends unknown ? Omit<T, 'area' | 'projectId' | 'itemId' | 'key'> : never;

// What the sheet quotes, its quick answers, the title and the placeholder, for each mode.
interface Frame {
  title: string;
  quote: string;
  options: string[];
  placeholder: string;
  where: string;
}

function frameOf({ mode, item, ask }: AnswerSheetRequest): Frame {
  const posted = `Posted on ${item.key}.`;
  const reads = 'The next agent to pick it up reads it.';
  switch (mode) {
    case 'question': {
      const q = ask.question;
      if (q === null) throw new Error('A question sheet needs the question ask.');
      return {
        title: 'Answer',
        quote: q.text,
        options: q.options,
        placeholder: 'Type an answer',
        where: `${posted} ${reads}`,
      };
    }
    case 'mention': {
      const m = ask.mention;
      if (m === null) throw new Error('A mention sheet needs the mention ask.');
      return {
        title: 'Reply',
        quote: m.excerpt,
        options: [],
        placeholder: 'Type an answer',
        where: `${posted} It starts with their name. ${reads}`,
      };
    }
    case 'discuss': {
      const d = ask.decision;
      if (d === null) throw new Error('A discuss sheet needs the decision ask.');
      return {
        title: `Discuss D${d.number}`,
        quote: `${d.question}\nRecommended: ${d.recommended}`,
        options: [],
        placeholder: 'Type an answer',
        where: `${posted} The decision stays open. ${reads}`,
      };
    }
    case 'status': {
      const s = ask.status;
      if (s === null) throw new Error('A status sheet needs the status ask.');
      return {
        title: 'New status',
        quote: s.statusLine,
        options: [],
        placeholder: 'Type an answer',
        where: `${posted} It replaces the status line. ${reads}`,
      };
    }
  }
}

// The status line is one line of at most 500 characters.
function statusProblem(text: string, body: string): string | null {
  if (/[\r\n]/.test(text)) return 'The status is one line: remove the line breaks.';
  if (body.length > STATUS_MAX) return `The status is at most ${STATUS_MAX} characters.`;
  return null;
}

function actionOf(
  { mode, item, ask }: AnswerSheetRequest,
  body: string,
  quick: number | null,
): Detail<InboxAction> {
  switch (mode) {
    case 'question': {
      const q = ask.question;
      if (q === null) throw new Error('A question sheet needs the question ask.');
      return quick !== null && q.options[quick] === body
        ? { kind: 'chooseOption', number: q.number, optionIndex: quick, option: body }
        : { kind: 'answer', number: q.number, body };
    }
    case 'mention': {
      const m = ask.mention;
      if (m === null) throw new Error('A mention sheet needs the mention ask.');
      return { kind: 'mentionReply', mentionId: m.mentionId, label: ask.label, body };
    }
    case 'discuss': {
      const d = ask.decision;
      if (d === null) throw new Error('A discuss sheet needs the decision ask.');
      return { kind: 'discuss', number: d.number, body };
    }
    case 'status': {
      const s = ask.status;
      if (s === null) throw new Error('A status sheet needs the status ask.');
      return { kind: 'newStatus', revision: item.revision, text: body, from: s.statusLine };
    }
  }
}

// One ask answered in a sheet: the ask quoted, quick answers, where the answer goes, then typing or the mic.
// Apply dispatches one Inbox action and closes; the runner's toast carries Undo.
export function AnswerSheet({ request, onClose }: AnswerSheetProps) {
  const theme = useTheme();
  const { colors } = theme;
  const actions = useInboxActions();
  const frame = frameOf(request);
  const [text, setText] = useState('');
  // The quick answer last tapped; it counts as sent unchanged while the box still holds exactly its text.
  const [quick, setQuick] = useState<number | null>(null);
  const body = text.trim();
  const problem = request.mode === 'status' ? statusProblem(text, body) : null;
  const off = body === '' || problem !== null;

  const apply = () => {
    if (off) return;
    actions.dispatch({
      area: 'inbox',
      projectId: request.projectId,
      itemId: request.item.id,
      key: request.item.key,
      ...actionOf(request, body, quick),
    } as InboxAction);
    onClose();
  };

  return (
    <BottomSheet title={frame.title} open onClose={onClose} testID={inboxIds.answerSheet}>
      <KeyboardAvoidingView
        testID={`${inboxIds.answerSheet}-keyboard`}
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        style={{ gap: theme.space[3] }}
      >
        <Text style={{ color: colors.muted, fontSize: theme.font.size.sm }}>
          {request.item.key} {request.item.title}
        </Text>
        {frame.quote === '' ? null : (
          <View
            testID={inboxIds.answerQuote}
            style={{
              padding: theme.space[3],
              borderRadius: theme.radius.md,
              backgroundColor: colors.surface2,
            }}
          >
            <Text style={{ color: colors.text, fontSize: theme.font.size.body15 }}>
              {frame.quote}
            </Text>
          </View>
        )}
        {frame.options.length > 0 ? (
          <View style={{ gap: theme.space[2] }}>
            <Text style={{ color: colors.muted, fontSize: theme.font.size.sm }}>
              Tap to answer with just this
            </Text>
            <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: theme.space[2] }}>
              {frame.options.map((option, index) => (
                <Pressable
                  key={index}
                  testID={answerQuickId(index)}
                  accessibilityRole="button"
                  onPress={() => {
                    setText(option);
                    setQuick(index);
                  }}
                  style={{
                    minHeight: theme.touch.control,
                    justifyContent: 'center',
                    paddingHorizontal: theme.space[3],
                    borderRadius: theme.radius.md,
                    borderWidth: 1,
                    borderColor: quick === index ? colors.accent : colors.border,
                    backgroundColor: quick === index ? colors.accentSoft : colors.surface,
                  }}
                >
                  <Text style={{ color: colors.text, fontSize: theme.font.size.body15 }}>
                    {option}
                  </Text>
                </Pressable>
              ))}
            </View>
          </View>
        ) : null}
        <Text
          testID={inboxIds.answerWhere}
          style={{ color: colors.muted, fontSize: theme.font.size.body15 }}
        >
          {frame.where}
        </Text>
        <View style={{ flexDirection: 'row', alignItems: 'flex-end', gap: theme.space[2] }}>
          <TextInput
            testID={inboxIds.answerField}
            accessibilityLabel={frame.placeholder}
            accessibilityHint={problem ?? undefined}
            multiline
            value={text}
            onChangeText={setText}
            placeholder={frame.placeholder}
            placeholderTextColor={colors.muted}
            style={{
              flex: 1,
              minHeight: theme.touch.control,
              paddingHorizontal: theme.space[3],
              paddingVertical: theme.space[2],
              borderRadius: theme.radius.md,
              borderWidth: 1,
              borderColor: problem === null ? colors.border : colors.danger,
              backgroundColor: colors.surface,
              color: colors.text,
              fontSize: theme.font.size.input,
            }}
          />
          <MicSlot placement="dock" />
        </View>
        {problem !== null ? (
          <Text
            testID={inboxIds.answerError}
            accessibilityRole="alert"
            style={{ color: colors.danger, fontSize: theme.font.size.body15 }}
          >
            {problem}
          </Text>
        ) : null}
        <Button label="Apply" onPress={apply} disabled={off} testID={inboxIds.answerApply} />
      </KeyboardAvoidingView>
    </BottomSheet>
  );
}

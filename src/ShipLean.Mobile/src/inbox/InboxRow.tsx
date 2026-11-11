import type { InboxAskDto, InboxItemDto } from '@shiplean/core/api/inbox';
import { Pressable, Text, View } from 'react-native';
import { typeName } from '@shiplean/core/api/workItemTypes';
import { Avatar } from '../ui/Avatar';
import { Button } from '../ui/Button';
import { Icon } from '../ui/Icon';
import { TypeChip } from '../ui/TypeChip';
import { layout } from '../ui/layout';
import { useTheme } from '../theme';
import type { PendingAnswer } from './actions';
import { useInboxActions } from './InboxActions';
import { useInboxSheets } from './InboxSheets';
import {
  buttonsFor,
  fromAvatar,
  fromLine,
  kindDetail,
  kindIcon,
  kindLabel,
  moreLine,
  pendingFor,
  pendingLine,
  splitAsks,
  waitsLine,
  type AskButton,
} from './rowModel';
import {
  inboxAgeId,
  inboxAskId,
  inboxBtnId,
  inboxFromId,
  inboxKindDetailId,
  inboxKindId,
  inboxMoreId,
  inboxOpenId,
  inboxPendingId,
  inboxRowId,
} from './testIds';

export interface InboxRowProps {
  projectId: string;
  item: InboxItemDto;
  // This item's answers still in the runner (held, queued, sending, uncertain or failed).
  pending: PendingAnswer[];
  // A Story's row under its Feature's.
  indented: boolean;
  // The item's other asks opened in place.
  expanded: boolean;
  onToggleExpanded: () => void;
  // A tap on the row, not on a button: opens links.item(projectKey, item.key).
  onOpenItem: () => void;
}

// What an ask shows as its text: the question, the quoted mention or status line, and for a batch the first decision
// and how many more.
function askText(asks: InboxAskDto[]): { text: string; extra: string | null } {
  const ask = asks[0]!;
  if (ask.decision !== null)
    return {
      text: ask.decision.question,
      extra: asks.length > 1 ? `and ${asks.length - 1} more` : null,
    };
  if (ask.question !== null) return { text: ask.question.text, extra: null };
  if (ask.mention !== null) return { text: `“${ask.mention.excerpt}”`, extra: null };
  return { text: `“${ask.status!.statusLine}”`, extra: null };
}

export function InboxRow({
  projectId,
  item,
  pending,
  indented,
  expanded,
  onToggleExpanded,
  onOpenItem,
}: InboxRowProps) {
  const theme = useTheme();
  const { colors } = theme;
  const { dispatch } = useInboxActions();
  const sheets = useInboxSheets();
  const { first, rest } = splitAsks(item);
  const more = moreLine(rest);
  const waits = waitsLine(item);
  const text = { fontSize: theme.font.size.body15, color: colors.text, lineHeight: 20 };
  const small = { fontSize: theme.font.size.sm, color: colors.muted };

  const target = { area: 'inbox', projectId, itemId: item.id, key: item.key } as const;
  const press = (asks: InboxAskDto[], button: AskButton) => {
    const ask = asks[0]!;
    const intent = button.intent;
    switch (intent.kind) {
      case 'approve':
        dispatch({ ...target, kind: 'approve', numbers: [intent.number] });
        return;
      case 'review':
        sheets.openReview(item, { scrollToConfirm: intent.scrollToConfirm, projectId });
        return;
      case 'option':
        dispatch({
          ...target,
          kind: 'chooseOption',
          number: intent.number,
          optionIndex: intent.optionIndex,
          option: intent.option,
        });
        return;
      case 'seen':
        dispatch({ ...target, kind: 'seen', mentionId: intent.mentionId, label: ask.label });
        return;
      case 'stillTrue':
        dispatch({ ...target, kind: 'stillTrue', revision: item.revision });
        return;
      case 'sheet':
        sheets.openAnswer({ mode: intent.mode, item, ask, projectId });
        return;
    }
  };

  const block = (asks: InboxAskDto[], opts: { withWaits: boolean; sub: boolean }) => {
    const head = asks[0]!;
    const { text: body, extra } = askText(asks);
    const answer = pendingFor(
      asks.map((a) => a.label),
      pending,
    );
    const inFlight = answer !== null && answer.state !== 'failed';
    const who = fromLine(head);
    const avatar = fromAvatar(head);
    const detail = kindDetail(asks);
    return (
      <View
        key={head.label}
        testID={inboxAskId(item.key, head.label)}
        style={
          opts.sub
            ? {
                marginTop: theme.space[3],
                paddingTop: 2,
                borderTopWidth: 1,
                borderTopColor: colors.border,
                borderStyle: 'dashed',
              }
            : undefined
        }
      >
        <View
          style={{
            flexDirection: 'row',
            alignItems: 'center',
            gap: 6,
            marginTop: theme.space[2] + 2,
          }}
        >
          <Icon name={kindIcon(asks)} size={16} color={colors.warn} />
          <Text
            testID={inboxKindId(item.key, head.label)}
            style={{
              color: colors.warn,
              fontSize: theme.font.size.xs,
              fontWeight: theme.font.weight.bold,
              letterSpacing: 0.5,
              textTransform: 'uppercase',
            }}
          >
            {kindLabel(asks)}
          </Text>
          {detail === null ? null : (
            <Text
              testID={inboxKindDetailId(item.key, head.label)}
              style={{ color: colors.muted, fontSize: theme.font.size.xs }}
            >
              {`· ${detail}`}
            </Text>
          )}
        </View>
        <Text style={[text, { marginTop: 4 }]}>{body}</Text>
        {extra === null ? null : <Text style={[small, { marginTop: 2 }]}>{extra}</Text>}
        {head.decision !== null && asks.length === 1 ? (
          <Text style={[small, { marginTop: 4 }]}>
            {'Recommended: '}
            <Text style={{ color: colors.text, fontWeight: theme.font.weight.medium }}>
              {head.decision.recommended}
            </Text>
          </Text>
        ) : null}
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: 6, marginTop: 6 }}>
          {avatar === null ? null : <Avatar user={avatar.user} agent={avatar.agent} size="sm" />}
          <Text testID={inboxFromId(item.key, head.label)} style={[small, { flex: 1 }]}>
            {[who, opts.withWaits ? waits : null].filter((s) => s !== null && s !== '').join(' · ')}
          </Text>
        </View>
        {answer !== null ? (
          <Text
            testID={inboxPendingId(item.key, head.label)}
            style={[small, { marginTop: theme.space[2] + 2 }]}
          >
            {pendingLine(answer)}
          </Text>
        ) : null}
        {inFlight ? null : (
          <View
            style={{
              flexDirection: 'row',
              flexWrap: 'wrap',
              gap: theme.space[2],
              marginTop: theme.space[2] + 2,
            }}
          >
            {buttonsFor(asks).map((button) => (
              <Button
                key={button.id}
                testID={inboxBtnId(item.key, head.label, button.id)}
                label={button.label}
                variant={button.primary ? 'primary' : 'soft'}
                icon={button.icon}
                onPress={() => press(asks, button)}
              />
            ))}
          </View>
        )}
      </View>
    );
  };

  return (
    <Pressable
      testID={inboxRowId(item.key)}
      // The header below is the row's way in for a screen reader; the buttons inside must stay reachable.
      accessible={false}
      onPress={onOpenItem}
      data-indented={indented}
      style={{
        paddingTop: theme.space[3],
        paddingBottom: theme.space[3] + 2,
        paddingLeft: indented ? 32 : theme.space[4],
        paddingRight: theme.space[4],
        borderBottomWidth: 1,
        borderBottomColor: colors.border,
        backgroundColor: colors.surface,
      }}
    >
      <Pressable
        testID={inboxOpenId(item.key)}
        accessibilityRole="button"
        accessibilityLabel={`Open ${item.key}, ${item.title}`}
        onPress={onOpenItem}
        style={{ flexDirection: 'row', alignItems: 'center', gap: 6, minWidth: 0 }}
      >
        <TypeChip type={{ id: item.type, name: typeName(item.type) }} />
        <Text style={{ ...small, fontFamily: theme.font.mono }}>{item.key}</Text>
        <Text
          numberOfLines={1}
          style={{
            flex: 1,
            color: colors.text,
            fontSize: theme.font.size.sm,
            fontWeight: theme.font.weight.semibold,
          }}
        >
          {item.title}
        </Text>
        <Text
          testID={inboxAgeId(item.key)}
          style={{
            color: item.longWait ? colors.warn : colors.muted,
            fontSize: theme.font.size.xs,
            fontWeight: item.longWait ? theme.font.weight.semibold : theme.font.weight.regular,
          }}
        >
          {item.ageLabel}
        </Text>
      </Pressable>
      {block(first, { withWaits: true, sub: false })}
      {expanded ? rest.map((ask) => block([ask], { withWaits: false, sub: true })) : null}
      {more === null ? null : (
        <Pressable
          testID={inboxMoreId(item.key)}
          accessibilityRole="button"
          accessibilityState={{ expanded }}
          onPress={expanded ? onOpenItem : onToggleExpanded}
          style={{
            minHeight: layout.compact + layout.compactSlop * 2,
            flexDirection: 'row',
            alignItems: 'center',
            marginTop: theme.space[2] + 2,
            borderTopWidth: 1,
            borderTopColor: colors.border,
            borderStyle: 'dashed',
          }}
        >
          <Text
            style={{
              flex: 1,
              color: colors.accent,
              fontSize: theme.font.size.sm,
              fontWeight: theme.font.weight.medium,
            }}
          >
            {expanded ? `Open ${item.key}` : more}
          </Text>
        </Pressable>
      )}
    </Pressable>
  );
}

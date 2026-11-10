// The dock's composer (typed fallback D1-D4, D8-D10): "Type a note" opens it. Its Note | Command switch, route line
// and Send label come from one mode (mode.ts). A note posts at once through the runner, word for word, with the typed
// undo window; offline the runner queues it. Command is CommandComposer's. The text is kept as a draft per item.
import { getMembers, projectMetaKeys } from '@shiplean/core/api/projectMeta';
import { platform } from '@shiplean/core/platform';
import { skipToken, useQuery } from '@tanstack/react-query';
import { NavigationContext } from 'expo-router/react-navigation';
import {
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  useSyncExternalStore,
  type ReactNode,
} from 'react';
import { Keyboard, Pressable, Text, TextInput, View } from 'react-native';
import { useActionRunner } from '../actions/ActionRunnerProvider';
import { scopeOf } from '../actions/scope';
import { UNDO } from '../actions/undo';
import { useSession } from '../session/useSession';
import { useTheme } from '../theme';
import { Chip } from '../ui/Chip';
import { layout } from '../ui/layout';
import { primitiveIds } from '../ui/testIds';
import { useVoiceInternals } from '../voice/internals';
import { CommandComposer } from '../voice/typed/CommandComposer';
import {
  useComposerRequest,
  type ComposerReason,
  type ComposerRequest,
} from '../voice/typed/composerRequest';
import { useVoice } from '../voice/VoiceProvider';
import type { WorkItemAction } from '../workItem/actions';
import { clearDraft, loadDraft, saveDraft } from './drafts';
import { applyMention, mentionAt, suggestMentions } from './mentions';
import { modeView, type ComposerMode } from './mode';
import { nudgeFor, nudgeTitle } from './nudge';
import { dockIds, mentionOptionId } from './testIds';

export interface DockComposerProps {
  itemId?: string;
  itemKey?: string;
  projectId?: string;
  placeholder: string;
  onSubmitted?: () => void;
  // The mic, beside the closed field.
  mic: ReactNode;
}

// Whether this screen is the focused one. A stack keeps earlier screens mounted, so only the focused screen's dock
// may take a composer request. Outside a navigator there is no focus to lose, so it counts as focused.
function useScreenFocused(): boolean {
  const nav = useContext(NavigationContext);
  const subscribe = useCallback(
    (onChange: () => void) => {
      if (nav === undefined) return () => undefined;
      const offFocus = nav.addListener('focus', onChange);
      const offBlur = nav.addListener('blur', onChange);
      return () => {
        offFocus();
        offBlur();
      };
    },
    [nav],
  );
  return useSyncExternalStore(subscribe, () => nav?.isFocused() ?? true);
}

const MODE_LABEL: Record<ComposerMode, string> = { note: 'Note', command: 'Command' };

export function DockComposer({
  itemId,
  itemKey,
  projectId,
  placeholder,
  onSubmitted,
  mic,
}: DockComposerProps) {
  const theme = useTheme();
  const { colors } = theme;
  const runner = useActionRunner();
  const voice = useVoice();
  // The focused screen's registered spec gives the nudge its words.
  const internals = useVoiceInternals();
  const focused = useScreenFocused();
  const { request, consume } = useComposerRequest();
  const { origin, email } = useSession();
  const scope = scopeOf({ origin, email });
  const store = platform().persistentStore();

  // Opens on Note, or on the draft's mode; the choice lasts until the screen is left (this component unmounts).
  const [initial] = useState(() =>
    scope === null || itemId === undefined ? null : loadDraft(store, scope, itemId),
  );
  const [text, setText] = useState(initial?.text ?? '');
  const [chosen, setChosen] = useState<ComposerMode>(initial?.mode ?? 'note');
  const [reason, setReason] = useState<ComposerReason>(null);
  const [open, setOpen] = useState(false);
  const [caret, setCaret] = useState(text.length);

  const view = modeView(chosen, voice.state);

  // Type instead and Edit open the composer on Command with their text; nothing else switches the mode. The state is
  // taken while rendering (once per request), and the request is consumed after. Only the focused screen's dock
  // takes it: an earlier screen in the stack is still mounted and must not open or keep a draft.
  // TODO: 'note' and 'reply' requests are taken by SHIP-1253 (dock reply mode).
  const [taken, setTaken] = useState<ComposerRequest | null>(null);
  if (focused && request !== null && request.mode === 'command' && request !== taken) {
    setTaken(request);
    setChosen('command');
    setText(request.text);
    setCaret(request.text.length);
    setReason(request.reason);
    setOpen(true);
  }
  useEffect(() => {
    if (focused && taken !== null && request === taken) consume();
  }, [focused, taken, request, consume]);

  // The draft follows the text and its mode; blank text clears it.
  useEffect(() => {
    if (scope === null || itemId === undefined) return;
    saveDraft(store, scope, itemId, { text, mode: chosen });
  }, [store, scope, itemId, text, chosen]);

  // Closing the keyboard closes the composer and leaves the draft in the dock.
  useEffect(() => {
    if (!open) return;
    const sub = Keyboard.addListener('keyboardDidHide', () => setOpen(false));
    return () => sub.remove();
  }, [open]);

  const members = useQuery({
    queryKey: projectMetaKeys.members(projectId ?? ''),
    queryFn: projectId === undefined || projectId === '' ? skipToken : () => getMembers(projectId),
  });
  const people = useMemo(() => (members.data ?? []).map((m) => m.user), [members.data]);
  const mention = view.mode === 'note' ? mentionAt(text, caret) : null;
  const hits = mention === null ? [] : suggestMentions(people, mention.query);

  const hotwords = internals.spec()?.hotwords() ?? [];
  const nudge = view.mode === 'note' ? nudgeFor(text, hotwords) : null;

  const canPost = text.trim() !== '' && itemId !== undefined && itemKey !== undefined;

  const change = (next: string) => {
    setText(next);
    setCaret(next.length);
  };

  const finish = () => {
    setText('');
    setCaret(0);
    setReason(null);
    if (scope !== null && itemId !== undefined) clearDraft(store, scope, itemId);
    setOpen(false);
    Keyboard.dismiss();
  };

  const post = () => {
    if (!canPost) return;
    const note: WorkItemAction = {
      area: 'workItem',
      itemId,
      key: itemKey,
      kind: 'addNote',
      body: text,
    };
    runner.dispatch([note], { undo: UNDO.typed, origin: 'typed', allOrNothing: true });
    finish();
    onSubmitted?.();
  };

  const previewInstead = () => {
    voice.submitTyped(text);
    setChosen('command');
    finish();
  };

  if (!open) {
    const draft = text.trim() === '' ? null : `${MODE_LABEL[view.mode]} draft`;
    return (
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: layout.dockGap }}>
        <Pressable
          testID={dockIds.open}
          accessibilityRole="button"
          accessibilityLabel={draft === null ? placeholder : `${draft}: ${text}`}
          onPress={() => setOpen(true)}
          style={{ flex: 1, justifyContent: 'center' }}
        >
          <TextInput
            testID={primitiveIds.dockField}
            // The closed dock only shows the field (and the draft); pressing it opens the composer.
            editable={false}
            pointerEvents="none"
            value={text}
            numberOfLines={1}
            placeholder={placeholder}
            placeholderTextColor={colors.muted}
            accessibilityLabel={placeholder}
            style={{
              flex: 1,
              height: theme.touch.control,
              borderRadius: layout.dockFieldRadius,
              paddingLeft: theme.space[4],
              paddingRight: draft === null ? theme.space[4] : theme.space[4] + layout.dockDraftRoom,
              backgroundColor: colors.surface2,
              color: colors.text,
              fontSize: theme.font.size.body15,
            }}
          />
          {draft === null ? null : (
            <Text
              testID={dockIds.draftLabel}
              style={{
                position: 'absolute',
                right: theme.space[4],
                color: colors.muted,
                fontSize: theme.font.size.xs,
              }}
            >
              {draft}
            </Text>
          )}
        </Pressable>
        {mic}
      </View>
    );
  }

  const segment = (mode: ComposerMode, testID: string, enabled: boolean) => {
    const on = view.mode === mode;
    return (
      <Pressable
        testID={testID}
        accessibilityRole="tab"
        accessibilityState={{ selected: on, disabled: !enabled }}
        disabled={!enabled}
        hitSlop={{ top: layout.compactSlop, bottom: layout.compactSlop }}
        onPress={() => setChosen(mode)}
        style={{
          height: layout.compact,
          justifyContent: 'center',
          paddingHorizontal: theme.space[3],
          borderRadius: theme.radius.sm,
          backgroundColor: on ? colors.surface : 'transparent',
          opacity: enabled ? 1 : layout.disabledOpacity,
        }}
      >
        <Text
          style={{
            color: on ? (mode === 'command' ? colors.accent : colors.text) : colors.muted,
            fontSize: theme.font.size.base,
            fontWeight: theme.font.weight.medium,
          }}
        >
          {MODE_LABEL[mode]}
        </Text>
      </Pressable>
    );
  };

  return (
    <View testID={dockIds.composer} style={{ gap: theme.space[2] }}>
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: theme.space[2] }}>
        <View
          accessibilityRole="tablist"
          accessibilityLabel="Send as"
          style={{
            flexDirection: 'row',
            padding: layout.segmentedPadding,
            borderRadius: theme.radius.md,
            backgroundColor: colors.surface2,
          }}
        >
          {segment('note', dockIds.modeNote, view.segments.note.enabled)}
          {segment('command', dockIds.modeCommand, view.segments.command.enabled)}
        </View>
        {itemKey === undefined ? null : (
          <Text
            testID={dockIds.target}
            numberOfLines={1}
            style={{
              flex: 1,
              textAlign: 'right',
              color: colors.muted,
              fontSize: theme.font.size.xs,
            }}
          >
            on{' '}
            <Text style={{ color: colors.text, fontWeight: theme.font.weight.semibold }}>
              {itemKey}
            </Text>
          </Text>
        )}
      </View>
      {view.commandOff === null ? null : (
        <Text
          testID={dockIds.commandOff}
          style={{ color: colors.muted, fontSize: theme.font.size.xs }}
        >
          {view.commandOff}
        </Text>
      )}
      {view.mode === 'command' ? (
        <CommandComposer text={text} onChangeText={change} reason={reason} onSent={finish} />
      ) : (
        <>
          {nudge === null ? null : (
            <View
              testID={dockIds.nudge}
              style={{
                flexDirection: 'row',
                alignItems: 'center',
                gap: theme.space[2],
                padding: theme.space[2],
                paddingLeft: theme.space[3],
                borderRadius: theme.radius.lg,
                backgroundColor: colors.accentSoft,
              }}
            >
              <View style={{ flex: 1, minWidth: 0 }}>
                <Text
                  testID={dockIds.nudgeTitle}
                  style={{
                    color: colors.text,
                    fontSize: theme.font.size.sm,
                    fontWeight: theme.font.weight.semibold,
                  }}
                >
                  {nudgeTitle(nudge)}
                </Text>
                <Text
                  numberOfLines={1}
                  style={{ color: colors.muted, fontSize: theme.font.size.xs }}
                >
                  {nudge.changes.join(' · ')}
                </Text>
              </View>
              <Pressable
                testID={dockIds.nudgePreview}
                accessibilityRole="button"
                hitSlop={{ top: layout.compactSlop, bottom: layout.compactSlop }}
                onPress={previewInstead}
                style={{
                  height: layout.compact,
                  justifyContent: 'center',
                  paddingHorizontal: theme.space[3],
                  borderRadius: theme.radius.md,
                  backgroundColor: colors.accent,
                }}
              >
                <Text
                  style={{
                    color: colors.accentFg,
                    fontSize: theme.font.size.sm,
                    fontWeight: theme.font.weight.semibold,
                  }}
                >
                  Preview instead
                </Text>
              </Pressable>
            </View>
          )}
          <TextInput
            testID={dockIds.noteField}
            autoFocus
            multiline
            value={text}
            onChangeText={change}
            onSelectionChange={(e) => setCaret(e.nativeEvent.selection.end)}
            placeholder="Type a note. @ mentions someone"
            placeholderTextColor={colors.muted}
            accessibilityLabel="Note"
            style={{
              minHeight: theme.touch.control,
              borderWidth: layout.noteFieldBorder,
              borderColor: colors.accent,
              borderRadius: theme.radius.lg + layout.noteFieldRadiusExtra,
              paddingHorizontal: theme.space[3],
              paddingVertical: theme.space[2],
              backgroundColor: colors.surface,
              color: colors.text,
              fontSize: theme.font.size.input,
            }}
          />
          {mention === null || hits.length === 0 ? null : (
            <View
              testID={dockIds.mentions}
              style={{ flexDirection: 'row', flexWrap: 'wrap', gap: theme.space[2] }}
            >
              {hits.map((person) => (
                <Chip
                  key={person.id}
                  testID={mentionOptionId(person.id)}
                  label={person.name}
                  selected={false}
                  onPress={() => {
                    const next = applyMention(text, caret, mention, person);
                    setText(next.text);
                    setCaret(next.caret);
                  }}
                />
              ))}
            </View>
          )}
          <View style={{ flexDirection: 'row', alignItems: 'center', gap: theme.space[3] }}>
            {view.route === null ? null : (
              <Text
                testID={dockIds.route}
                style={{ flex: 1, color: colors.muted, fontSize: theme.font.size.sm }}
              >
                <Text style={{ color: colors.text, fontWeight: theme.font.weight.semibold }}>
                  {view.route.bold}
                </Text>
                {view.route.rest}
              </Text>
            )}
            <Pressable
              testID={dockIds.post}
              accessibilityRole="button"
              accessibilityLabel={view.send ?? 'Post'}
              accessibilityState={{ disabled: !canPost }}
              disabled={!canPost}
              onPress={post}
              style={{
                minHeight: theme.touch.control,
                justifyContent: 'center',
                paddingHorizontal: theme.space[4],
                borderRadius: theme.touch.control / 2,
                backgroundColor: canPost ? colors.accent : colors.surface2,
              }}
            >
              <Text
                style={{
                  color: canPost ? colors.accentFg : colors.muted,
                  fontSize: theme.font.size.body15,
                  fontWeight: theme.font.weight.semibold,
                }}
              >
                {view.send}
              </Text>
            </Pressable>
          </View>
        </>
      )}
    </View>
  );
}

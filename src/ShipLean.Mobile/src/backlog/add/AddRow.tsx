import { useEffect, useRef, useState } from 'react';
import { Text, TextInput, View } from 'react-native';
import { usePending } from '../../actions/ActionRunnerProvider';
import { UNDO } from '../../actions/undo';
import { useTheme } from '../../theme';
import { Segmented } from '../../ui/Segmented';
import type { BacklogAddItem, BacklogLevel } from '../actions';
import { useBacklogActions } from '../BacklogActions';
import { useInvalidateBacklog } from '../queries';
import { addRowErrorId, addRowId, addRowInputId, addRowLabelId, addRowTypeId } from '../testIds';
import { TOP } from './send';

export interface AddRowProps {
  projectId: string;
  level: BacklogLevel;
  parent: { id: string; key: string } | null;
}

type AddType = BacklogAddItem['type'];

// The web's text (LevelNewItemRow).
const PLACEHOLDER = 'Title, then Enter to add at the top';

// "+ New Feature" at the Features level; "+ New" with a Story / Bug toggle at the Stories level. Enter adds the title at
// the top through the runner (sent at once, Undo on the toast) and keeps the row open. The Epics level has no add row.
export function AddRow({ projectId, level, parent }: AddRowProps) {
  const theme = useTheme();
  const { colors } = theme;
  const backlog = useBacklogActions();
  const invalidate = useInvalidateBacklog();
  const [title, setTitle] = useState('');
  const [storyOrBug, setStoryOrBug] = useState<'story' | 'bug'>('story');
  const type: AddType = level === 'features' ? 'feature' : storyOrBug;

  // The batches this row dispatched, so it reacts only to its own sends.
  const batches = useRef(new Set<string>());
  const refreshed = useRef(new Set<string>());
  const mine = usePending((p) => batches.current.has(p.batchId));
  const [dismissed, setDismissed] = useState<string | null>(null);

  // Once an add is sent, the backlog is read again so the item appears at rank 1.
  useEffect(() => {
    for (const p of mine) {
      if (p.state !== 'sent' || refreshed.current.has(p.batchId)) continue;
      refreshed.current.add(p.batchId);
      void invalidate(projectId);
    }
  }, [mine, invalidate, projectId]);

  // The server's refusal of a typed add (a 400) shows under the row until the person types again.
  const failed = mine
    .filter((p) => p.state === 'failed' && p.failure === 'validation' && p.error !== null)
    .at(-1);
  const error = failed !== undefined && failed.id !== dismissed ? failed.error : null;

  function change(text: string) {
    setTitle(text);
    if (failed !== undefined) setDismissed(failed.id);
  }

  function add() {
    // The title is the text as typed, trimmed only at the ends.
    const text = title.trim();
    if (text === '') return;
    const batchId = backlog.dispatch(
      {
        area: 'backlog',
        kind: 'addItem',
        itemId: null,
        key: null,
        projectId,
        type,
        title: text,
        parentId: parent?.id ?? null,
        parentKey: parent?.key ?? null,
        placement: TOP,
      },
      { origin: 'typed', undo: UNDO.typed },
    );
    batches.current.add(batchId);
    setTitle('');
  }

  return (
    <View
      testID={addRowId}
      accessibilityLabel={parent === null ? 'Add' : `Add under ${parent.key}`}
      style={{
        gap: theme.space[2],
        padding: theme.space[3],
        borderBottomWidth: 1,
        borderBottomColor: colors.border,
      }}
    >
      {level === 'features' ? (
        <Text
          testID={addRowLabelId}
          style={{
            color: colors.accent,
            fontSize: theme.font.size.base,
            fontWeight: theme.font.weight.semibold,
          }}
        >
          + New Feature
        </Text>
      ) : (
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: theme.space[2] }}>
          <Text
            testID={addRowLabelId}
            style={{
              color: colors.accent,
              fontSize: theme.font.size.base,
              fontWeight: theme.font.weight.semibold,
            }}
          >
            + New
          </Text>
          <Segmented
            testID={addRowTypeId}
            value={storyOrBug}
            onChange={setStoryOrBug}
            options={[
              { value: 'story', label: 'Story' },
              { value: 'bug', label: 'Bug' },
            ]}
          />
        </View>
      )}
      <TextInput
        testID={addRowInputId}
        accessibilityLabel={`New ${type}`}
        accessibilityHint={error ?? undefined}
        value={title}
        onChangeText={change}
        onSubmitEditing={add}
        submitBehavior="submit"
        returnKeyType="done"
        placeholder={PLACEHOLDER}
        placeholderTextColor={colors.muted}
        style={{
          minHeight: theme.touch.control,
          paddingHorizontal: theme.space[3],
          borderRadius: theme.radius.md,
          borderWidth: 1,
          borderColor: error === null ? colors.border : colors.danger,
          backgroundColor: colors.surface,
          color: colors.text,
          fontSize: theme.font.size.input,
        }}
      />
      {error === null ? null : (
        <Text
          testID={addRowErrorId}
          accessibilityRole="alert"
          style={{ color: colors.danger, fontSize: theme.font.size.sm }}
        >
          {error}
        </Text>
      )}
    </View>
  );
}

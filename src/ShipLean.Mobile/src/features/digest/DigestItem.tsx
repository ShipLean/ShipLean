import type {
  DigestItemDto,
  DigestLineDto,
  DigestLineKind,
} from '@shiplean/core/api/featuresOverview';
import { Pressable, Text, View } from 'react-native';
import { useTheme } from '../../theme';
import {
  featureDigestItemId,
  featureDigestItemTypeId,
  featureDigestLineAgentTagId,
  featureDigestLineAtId,
  featureDigestLineId,
  featureDigestLineKindId,
  featureDigestLineTextId,
  featureDigestLineWhoId,
} from '../testIds';

// One mark per kind of change (feature-board.html .chg): the glyph is decoration, the label is what a reader hears.
const KINDS: Record<DigestLineKind, { glyph: string; label: string }> = {
  state: { glyph: '→', label: 'State' },
  health: { glyph: '♥\uFE0E', label: 'Health' },
  statusLine: { glyph: '¶', label: 'Status line' },
  comment: { glyph: '✉\uFE0E', label: 'Note' },
  agentWork: { glyph: '⚙\uFE0E', label: 'Agent work' },
  ask: { glyph: '?', label: 'Question' },
};

export interface DigestItemProps {
  item: DigestItemDto;
  onOpen: (key: string) => void;
}

// A work item with what happened to it: its type, key and title (a tap opens it), then one line per change.
export function DigestItem({ item, onOpen }: DigestItemProps) {
  const theme = useTheme();
  const { colors } = theme;
  return (
    <View
      testID={featureDigestItemId(item.key)}
      style={{
        paddingVertical: theme.space[3],
        paddingHorizontal: theme.space[4],
        gap: theme.space[2],
        backgroundColor: colors.surface,
        borderBottomWidth: 1,
        borderBottomColor: colors.border,
      }}
    >
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={`Open ${item.key}`}
        onPress={() => onOpen(item.key)}
        style={{
          minHeight: theme.touch.row,
          flexDirection: 'row',
          alignItems: 'center',
          gap: theme.space[2],
        }}
      >
        <Text
          testID={featureDigestItemTypeId(item.key)}
          style={{ color: colors.muted, fontSize: theme.font.size.sm }}
        >
          {item.type}
        </Text>
        <Text
          style={{ color: colors.muted, fontSize: theme.font.size.sm, fontFamily: theme.font.mono }}
        >
          {item.key}
        </Text>
        <Text
          numberOfLines={2}
          style={{
            flex: 1,
            color: colors.text,
            fontSize: theme.font.size.body15,
            fontWeight: theme.font.weight.semibold,
          }}
        >
          {item.title}
        </Text>
      </Pressable>
      {item.lines.map((line, i) => (
        <DigestLine key={`${line.at}-${i}`} itemKey={item.key} index={i} line={line} />
      ))}
    </View>
  );
}

interface DigestLineProps {
  itemKey: string;
  index: number;
  line: DigestLineDto;
}

function DigestLine({ itemKey, index, line }: DigestLineProps) {
  const theme = useTheme();
  const { colors } = theme;
  const kind = KINDS[line.kind];
  const who = line.agentName ?? line.by?.name ?? null;
  const small = { color: colors.muted, fontSize: theme.font.size.sm } as const;
  return (
    <View
      testID={featureDigestLineId(itemKey, index)}
      style={{ flexDirection: 'row', gap: theme.space[2] }}
    >
      <Text
        testID={featureDigestLineKindId(itemKey, index)}
        accessibilityLabel={kind.label}
        style={{
          width: 20,
          textAlign: 'center',
          color: colors.accent,
          fontSize: theme.font.size.md,
        }}
      >
        {kind.glyph}
      </Text>
      <View style={{ flex: 1, gap: theme.space[1] }}>
        <Text
          testID={featureDigestLineTextId(itemKey, index)}
          style={{ color: colors.text, fontSize: theme.font.size.md }}
        >
          {line.text}
        </Text>
        <View
          style={{
            flexDirection: 'row',
            flexWrap: 'wrap',
            alignItems: 'center',
            gap: theme.space[2],
          }}
        >
          {who === null ? null : (
            <Text testID={featureDigestLineWhoId(itemKey, index)} style={small}>
              {who}
            </Text>
          )}
          {line.agentName === null ? null : (
            <View
              testID={featureDigestLineAgentTagId(itemKey, index)}
              style={{
                height: 20,
                paddingHorizontal: 7,
                borderRadius: 10,
                justifyContent: 'center',
                backgroundColor: colors.accentSoft,
              }}
            >
              <Text
                style={{
                  fontSize: theme.font.size.xs,
                  fontWeight: theme.font.weight.semibold,
                  color: colors.text,
                }}
              >
                Agent
              </Text>
            </View>
          )}
          <Text testID={featureDigestLineAtId(itemKey, index)} style={small}>
            {line.atLabel}
          </Text>
        </View>
      </View>
    </View>
  );
}

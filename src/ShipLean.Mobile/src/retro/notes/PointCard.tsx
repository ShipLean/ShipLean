import type { RetroPointDto } from '@shiplean/core/api/retros';
import { Pressable, Text, View } from 'react-native';
import { useTheme } from '../../theme';
import { layout, tint } from '../../ui/layout';
import { StatePill } from '../../ui/StatePill';
import {
  retroActionKeyId,
  retroAddActionId,
  retroFaceId,
  retroFacesId,
  retroMoreFacesId,
  retroNotNeededId,
  retroNotNeededReasonId,
  retroPlusId,
  retroPointAuthorId,
  retroPointId,
  retroPointTextId,
} from '../testIds';
import { Face } from './Face';
import { authorLabel } from './notesModel';

const FACES = 3;
const FACE = 20;

export interface PointCardProps {
  point: RetroPointDto;
  // The point "that" refers to (D8).
  highlighted: boolean;
  // Any tap on the card, +1 included.
  onTouch: (point: RetroPointDto) => void;
  onPlus: (point: RetroPointDto) => void;
  onAddAction: (point: RetroPointDto) => void;
  onNotNeeded: (point: RetroPointDto) => void;
  onOpenItem: (key: string) => void;
}

// One retro point (retro.html .rnote): who wrote it, the item it is about, the text word for word, +1 with the
// supporters' faces, and its decision.
export function PointCard({
  point,
  highlighted,
  onTouch,
  onPlus,
  onAddAction,
  onNotNeeded,
  onOpenItem,
}: PointCardProps) {
  const theme = useTheme();
  const { colors } = theme;
  const author = authorLabel(point.author);
  const supporters = point.supporters;
  const more = supporters.length - FACES;
  const small = { fontSize: theme.font.size.sm, color: colors.muted } as const;
  const compact = (label: string, onPress: () => void, testID: string, look: 'solid' | 'plain') => (
    <Pressable
      key={testID}
      testID={testID}
      accessibilityRole="button"
      hitSlop={{ top: layout.compactSlop, bottom: layout.compactSlop }}
      onPress={onPress}
      style={{
        height: layout.compact,
        paddingHorizontal: theme.space[3],
        borderRadius: theme.radius.md,
        borderWidth: 1,
        borderColor: look === 'solid' ? colors.accent : colors.border,
        backgroundColor: look === 'solid' ? colors.accent : 'transparent',
        alignItems: 'center',
        justifyContent: 'center',
      }}
    >
      <Text
        style={{
          fontSize: theme.font.size.base,
          fontWeight: theme.font.weight.semibold,
          color: look === 'solid' ? colors.accentFg : colors.muted,
        }}
      >
        {label}
      </Text>
    </Pressable>
  );

  const decision = () => {
    if (point.decision === null) return null;
    if (point.decision === 'actioned' && point.action !== null) {
      const { action } = point;
      return (
        <Pressable
          testID={retroActionKeyId(point.id)}
          accessibilityRole="link"
          onPress={() => onOpenItem(action.key)}
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
      );
    }
    if (point.decision === 'notNeeded' && point.notNeeded !== null) {
      return (
        <View testID={retroNotNeededReasonId(point.id)} style={{ flexShrink: 1, gap: 2 }}>
          <Text
            style={{
              fontSize: theme.font.size.sm,
              fontWeight: theme.font.weight.semibold,
              color: colors.text,
            }}
          >
            Not needed
          </Text>
          <Text style={small}>{point.notNeeded.reason}</Text>
        </View>
      );
    }
    if (point.decision !== 'undecided') return null;
    return (
      <View style={{ flexDirection: 'row', gap: theme.space[2] }}>
        {compact('+ Action item', () => onAddAction(point), retroAddActionId(point.id), 'solid')}
        {compact('Not needed', () => onNotNeeded(point), retroNotNeededId(point.id), 'plain')}
      </View>
    );
  };

  return (
    <Pressable
      testID={retroPointId(point.id)}
      accessibilityState={{ selected: highlighted }}
      onPress={() => onTouch(point)}
      style={{
        gap: theme.space[2],
        paddingHorizontal: theme.space[4],
        paddingVertical: theme.space[3],
        backgroundColor: highlighted ? colors.accentSoft : colors.surface,
        borderLeftWidth: 3,
        borderLeftColor: highlighted ? colors.accent : 'transparent',
        borderBottomWidth: 1,
        borderBottomColor: colors.border,
      }}
    >
      <View
        testID={retroPointAuthorId(point.id)}
        style={{ flexDirection: 'row', alignItems: 'center', gap: theme.space[2] }}
      >
        <Face user={point.author.user} size={24} />
        <Text
          numberOfLines={1}
          style={{
            flexShrink: 1,
            fontSize: theme.font.size.sm,
            fontWeight: theme.font.weight.semibold,
            color: colors.text,
          }}
        >
          {author.name}
        </Text>
        {author.lane === null ? null : (
          <View
            style={{
              paddingHorizontal: theme.space[2],
              borderRadius: theme.radius.xs,
              backgroundColor: tint(colors.purple, 0.14),
            }}
          >
            <Text style={{ fontSize: theme.font.size.xs, color: colors.text }}>{author.lane}</Text>
          </View>
        )}
        <View style={{ flex: 1 }} />
        {point.workItem === null ? null : (
          <Text style={{ ...small, fontFamily: theme.font.mono }}>{point.workItem.key}</Text>
        )}
      </View>
      <Text
        testID={retroPointTextId(point.id)}
        style={{ fontSize: theme.font.size.body15, lineHeight: 21, color: colors.text }}
      >
        {point.text}
      </Text>
      <View
        style={{
          flexDirection: 'row',
          alignItems: 'center',
          flexWrap: 'wrap',
          gap: theme.space[2],
        }}
      >
        <Pressable
          testID={retroPlusId(point.id)}
          accessibilityRole="button"
          accessibilityLabel="+1"
          accessibilityState={{ selected: point.supportedByMe }}
          hitSlop={{ top: layout.compactSlop, bottom: layout.compactSlop }}
          onPress={() => onPlus(point)}
          style={{
            height: layout.compact,
            minWidth: 44,
            paddingHorizontal: theme.space[3],
            borderRadius: layout.compact / 2,
            borderWidth: 1,
            borderColor: point.supportedByMe ? 'transparent' : colors.border,
            backgroundColor: point.supportedByMe ? colors.accentSoft : 'transparent',
            alignItems: 'center',
            justifyContent: 'center',
          }}
        >
          <Text
            style={{
              fontSize: theme.font.size.sm,
              fontWeight: theme.font.weight.semibold,
              color: point.supportedByMe ? colors.accent : colors.muted,
            }}
          >
            {`👍 ${supporters.length || '+1'}`}
          </Text>
        </Pressable>
        {supporters.length === 0 ? null : (
          <View
            testID={retroFacesId(point.id)}
            style={{ flexDirection: 'row', alignItems: 'center' }}
          >
            {supporters.slice(0, FACES).map((user, i) => (
              <View key={user.id} style={{ marginLeft: i === 0 ? 0 : -5 }}>
                <Face user={user} size={FACE} testID={retroFaceId(point.id, user.id)} />
              </View>
            ))}
            {more > 0 ? (
              <Text testID={retroMoreFacesId(point.id)} style={{ ...small, marginLeft: 4 }}>
                +{more}
              </Text>
            ) : null}
          </View>
        )}
        <View style={{ flex: 1 }} />
        {decision()}
      </View>
    </Pressable>
  );
}

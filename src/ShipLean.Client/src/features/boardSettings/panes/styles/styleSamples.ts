import type { BoardLevel } from '../../../../api/boards';
import type {
  BoardSettingsJson,
  CardFieldSettingJson,
  StyleRuleJson,
} from '../../../../api/persisted';
import type { WorkItemTypeId } from '../../../../api/refs';
import type { CardDto } from '../../../../api/views';
import { fieldValuesOf, matches, type FieldValues } from '../../../../criteria/matches';
import { checkCriteria } from '../../../../criteria/validate';
import { previewCard, sampleRuleValues, SAMPLE_PEOPLE } from '../fields/samplePreview';
import { colourOfTag } from '../tags/tagEdits';

/** The signed-in user: `@me` in a rule stands for them, and one sample is assigned to them. */
export interface Viewer {
  id: string;
  name: string;
  initials: string;
  avatarColour: string;
  photoUrl: string | null;
}

/** Fields a sample card cannot answer for (it has no real parent, state or import origin): a rule that reads one is captioned, not previewed. */
const UNPREVIEWABLE = ['id', 'state', 'parent', 'epic', 'originalSystem', 'originalSystemId'];

interface Sample {
  id: string;
  key: string;
  title: string;
  tags: string[];
  priority: number;
  points: number | null;
  /** Who the card is assigned to: nobody, the signed-in user, or the other sample person. */
  assignedTo: 'nobody' | 'me' | 'mei';
}

/** Made-up cards that cover the usual rules (tags, priority, assignee); two of them can match more than one rule. The type of each follows the board's level. */
const SAMPLES: Sample[] = [
  {
    id: 'sample-1',
    key: 'KEY-129',
    title: 'Sign in with passkey',
    tags: ['Blocked', 'Security'],
    priority: 1,
    points: 8,
    assignedTo: 'me',
  },
  {
    id: 'sample-2',
    key: 'KEY-144',
    title: 'Address autocomplete',
    tags: ['Spike'],
    priority: 2,
    points: 3,
    assignedTo: 'nobody',
  },
  {
    id: 'sample-3',
    key: 'KEY-160',
    title: 'Session expires during checkout',
    tags: ['Customer'],
    priority: 1,
    points: 2,
    assignedTo: 'mei',
  },
  {
    id: 'sample-4',
    key: 'KEY-141',
    title: 'Pay with saved card',
    tags: ['Checkout'],
    priority: 3,
    points: 5,
    assignedTo: 'nobody',
  },
];

/** The type of each sample card on a board of this level: a Stories board also has bugs. */
const TYPES_OF_LEVEL: Record<BoardLevel, WorkItemTypeId[]> = {
  stories: ['story', 'story', 'bug', 'story'],
  features: ['feature', 'feature', 'feature', 'feature'],
  epics: ['epic', 'epic', 'epic', 'epic'],
  taskboard: ['task', 'task', 'task', 'task'],
};

export interface SampleOutcome {
  card: CardDto;
  /** Every enabled rule the card satisfies, in rule order: the first one styles the card. */
  matched: StyleRuleJson[];
}

const plain = (type: WorkItemTypeId): CardFieldSettingJson => ({
  type,
  showKey: true,
  showAssignee: true,
  showTags: true,
  showPoints: true,
  showEmptyFields: false,
  extraFields: [],
});

/** A rule with a half-typed clause (a refused value) matches nothing, as the server would not accept it. */
const holds = (rule: StyleRuleJson, values: FieldValues, me: string) =>
  rule.enabled && checkCriteria(rule.criteria).length === 0 && matches(rule.criteria, values, me);

/** The first enabled valid rule the card satisfies styles it, and the tag colours tint its tags: the card as the board would draw it. */
export function styledOutcome(
  card: CardDto,
  hidden: FieldValues,
  settings: BoardSettingsJson,
  me: string,
): SampleOutcome {
  const tinted: CardDto = {
    ...card,
    tags: card.tags.map((t) => ({ ...t, colour: colourOfTag(settings.tagColours, t.name) })),
  };
  const values = { ...fieldValuesOf(tinted), ...hidden };
  const matched = settings.styleRules.filter((rule) => holds(rule, values, me));
  return { card: { ...tinted, styleRuleId: matched[0]?.id ?? null }, matched };
}

/** Each sample as the board would draw it under these settings. */
export function sampleOutcomes(
  settings: BoardSettingsJson,
  level: BoardLevel,
  viewer: Viewer,
): SampleOutcome[] {
  return SAMPLES.map((sample, index) => {
    const type = TYPES_OF_LEVEL[level][index];
    const setting = settings.cardFields.find((s) => s.type === type) ?? plain(type);
    const base = previewCard({ ...setting, showEmptyFields: false, extraFields: [] });
    const assignee = {
      nobody: null,
      me: viewer,
      mei: { ...SAMPLE_PEOPLE.mei, photoUrl: null },
    }[sample.assignedTo];
    const card: CardDto = {
      ...base,
      id: sample.id,
      key: sample.key,
      title: sample.title,
      assignee,
      points: sample.points,
      childPoints: null,
      sumWarn: false,
      parent: null,
      tags: sample.tags.map((name) => ({ name, colour: null })),
    };
    return styledOutcome(card, sampleRuleValues(type, sample.priority), settings, viewer.id);
  });
}

/** The enabled, valid rules that read a field the samples cannot answer for, with the fields: they match nothing in the preview. */
export function unpreviewableRules(settings: BoardSettingsJson) {
  return settings.styleRules.flatMap((rule) => {
    if (!rule.enabled || checkCriteria(rule.criteria).length > 0) return [];
    const fields = rule.criteria.clauses
      .map((c) => c.field)
      .filter((field) => UNPREVIEWABLE.includes(field));
    return fields.length === 0 ? [] : [{ rule, fields: [...new Set(fields)] }];
  });
}

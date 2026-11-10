// What the command model is given and how its answer is read. The grammar (llama.cpp GBNF) forces the output shape:
// a JSON array of calls, each naming one of the screen's actions with arguments from that action's schema. A note is
// never text from the model: an action with a noteArg returns where the note is in the transcript (noteSpan), and the
// app copies the words itself (copyNoteSpan).
import type {
  JsonSchema,
  ParsedCall,
  ScreenVoiceSpec,
  Transcript,
  VoiceActionSpec,
} from './contracts';

// A GBNF string literal matching exactly `text`.
function literal(text: string): string {
  return `"${text.replace(/\\/g, '\\\\').replace(/"/g, '\\"').replace(/\n/g, '\\n')}"`;
}

// The literal for a JSON value: the property "state" is literal('"state"').
const jsonLiteral = (value: unknown) => literal(JSON.stringify(value));

const property = (name: string, value: string) => `${jsonLiteral(name)} ws ":" ws ${value}`;

function asSchema(value: unknown, where: string): JsonSchema {
  if (typeof value !== 'object' || value === null || Array.isArray(value))
    throw new Error(`The schema of ${where} isn't an object.`);
  return value as JsonSchema;
}

// The value a schema allows. Enums and consts are spelled out, so the model can only say one of them.
function valueOf(schema: JsonSchema, where: string): string {
  if (Array.isArray(schema.enum)) {
    if (schema.enum.length === 0) throw new Error(`The enum of ${where} is empty.`);
    return `( ${schema.enum.map(jsonLiteral).join(' | ')} )`;
  }
  if ('const' in schema) return jsonLiteral(schema.const);
  const types = Array.isArray(schema.type) ? schema.type : [schema.type];
  const alternatives = types.map((type) => {
    switch (type) {
      case 'string':
        return 'string';
      case 'integer':
        return 'integer';
      case 'number':
        return 'number';
      case 'boolean':
        return '( "true" | "false" )';
      case 'null':
        return '"null"';
      case 'array': {
        const item = valueOf(asSchema(schema.items, `${where}[]`), `${where}[]`);
        return `"[" ws ( ${item} ( ws "," ws ${item} )* )? ws "]"`;
      }
      case 'object':
        return objectOf(schema, where, []);
      default:
        throw new Error(
          `The schema of ${where} has a type the grammar can't express: ${String(type)}.`,
        );
    }
  });
  return alternatives.length === 1 ? alternatives[0] : `( ${alternatives.join(' | ')} )`;
}

// An object with the schema's properties in their declared order, required ones first; `leave` names properties
// the model must not give (the note argument).
function objectOf(schema: JsonSchema, where: string, leave: string[]): string {
  const properties = (schema.properties ?? {}) as Record<string, unknown>;
  const required = new Set(Array.isArray(schema.required) ? (schema.required as string[]) : []);
  const names = Object.keys(properties).filter((n) => !leave.includes(n));
  const ordered = [
    ...names.filter((n) => required.has(n)),
    ...names.filter((n) => !required.has(n)),
  ];
  const parts = ordered.map((n) =>
    property(n, valueOf(asSchema(properties[n], `${where}.${n}`), `${where}.${n}`)),
  );
  const firstOptional = ordered.findIndex((n) => !required.has(n));
  const mandatory = firstOptional === -1 ? parts : parts.slice(0, firstOptional);
  const optional = firstOptional === -1 ? [] : parts.slice(firstOptional);
  const after = (from: number) =>
    optional
      .slice(from)
      .map((p) => `( ws "," ws ${p} )?`)
      .join(' ');

  let body: string;
  if (mandatory.length > 0) {
    body = [mandatory.join(' ws "," ws '), after(0)].filter((s) => s !== '').join(' ');
  } else if (optional.length > 0) {
    // No required property: any one may come first, and those after it may follow.
    body = `( ${optional.map((p, i) => [p, after(i + 1)].filter((s) => s !== '').join(' ')).join(' | ')} )?`;
  } else {
    body = '';
  }
  return body === '' ? '"{" ws "}"' : `"{" ws ${body} ws "}"`;
}

function callOf(action: VoiceActionSpec): string {
  const leave = action.noteArg === undefined ? [] : [action.noteArg];
  const parts = [
    property('action', jsonLiteral(action.name)),
    property('args', objectOf(action.args, action.name, leave)),
  ];
  if (action.noteArg !== undefined) parts.push(property('noteSpan', 'notespan'));
  return `"{" ws ${parts.join(' ws "," ws ')} ws "}"`;
}

// The shared rules, from llama.cpp's grammars/json.gbnf.
const PRIMITIVES = [
  'notespan ::= "{" ws "\\"start\\"" ws ":" ws natural ws "," ws "\\"end\\"" ws ":" ws natural ws "}"',
  'natural ::= "0" | [1-9] [0-9]{0,9}',
  'integer ::= "-"? ( "0" | [1-9] [0-9]{0,15} )',
  'number ::= integer ( "." [0-9]+ )? ( [eE] [-+]? [0-9]+ )?',
  'string ::= "\\"" ( [^"\\\\\\x7F\\x00-\\x1F] | "\\\\" ( ["\\\\/bfnrt] | "u" [0-9a-fA-F]{4} ) )* "\\""',
  'ws ::= | " " | "\\n" [ \\t]{0,20}',
];

// GBNF for a JSON array of the spec's calls; [] is allowed (nothing to do).
export function buildGrammar(spec: Pick<ScreenVoiceSpec, 'actions'>): string {
  if (spec.actions.length === 0)
    return ['root ::= ws "[" ws "]" ws', 'ws ::= | " " | "\\n" [ \\t]{0,20}'].join('\n');
  const rules = [
    'root ::= ws "[" ws ( call ( ws "," ws call )* )? ws "]" ws',
    `call ::= ${spec.actions.map((_, i) => `call-${i}`).join(' | ')}`,
    ...spec.actions.map((action, i) => `call-${i} ::= ${callOf(action)}`),
    ...PRIMITIVES,
  ];
  return rules.join('\n');
}

const isPlainObject = (v: unknown): v is Record<string, unknown> =>
  typeof v === 'object' && v !== null && !Array.isArray(v);

const isNatural = (v: unknown): v is number => Number.isInteger(v) && (v as number) >= 0;

function callFrom(value: unknown): ParsedCall | null {
  if (!isPlainObject(value)) return null;
  const keys = Object.keys(value);
  if (!keys.every((k) => k === 'action' || k === 'args' || k === 'noteSpan')) return null;
  if (typeof value.action !== 'string' || !isPlainObject(value.args)) return null;
  if (value.noteSpan === undefined) return { action: value.action, args: value.args };
  const span = value.noteSpan;
  if (!isPlainObject(span) || Object.keys(span).length !== 2) return null;
  if (!isNatural(span.start) || !isNatural(span.end)) return null;
  return { action: value.action, args: value.args, noteSpan: { start: span.start, end: span.end } };
}

// The model's calls, or [] when the text isn't that shape. It never repairs the text.
export function parseModelOutput(text: string): ParsedCall[] {
  let value: unknown;
  try {
    value = JSON.parse(text);
  } catch {
    return [];
  }
  if (!Array.isArray(value)) return [];
  const calls = value.map(callFrom);
  return calls.every((c): c is ParsedCall => c !== null) ? calls : [];
}

function actionLine(action: VoiceActionSpec): string {
  const note =
    action.noteArg === undefined
      ? ''
      : ` Its ${action.noteArg} is never written: give noteSpan, where the note is in the command.`;
  return `- ${action.name}: ${action.description} Args: ${JSON.stringify(action.args)}.${note}`;
}

// The system prompt for a screen: short instructions and the action list. It depends only on the screen and its
// actions, so the phone evaluates it once per screen and keeps it (the cached prefix). The screen's context changes
// between commands, so it goes in the user turn.
export function buildSystemPrompt(spec: Pick<ScreenVoiceSpec, 'screen' | 'actions'>): string {
  return [
    `You turn one spoken command into calls to the actions of ShipLean's ${spec.screen} screen.`,
    'Reply with a JSON array of calls and nothing else: [{"action": "<name>", "args": {...}}]. Reply [] when the command asks for none of these actions.',
    'Rules:',
    '- Use only the actions below, with the arguments their schemas allow.',
    '- Take item keys, people and states from the screen context in the user message. Never invent one.',
    '- A command can ask for several things: give one call for each, in the order they were said.',
    '- noteSpan is {"start": s, "end": e}: character offsets in the command, counted from 0, with e one past the last character of the note. The note is the words after "note", "comment" or "say" that are to be posted as they were said.',
    'Actions:',
    ...spec.actions.map(actionLine),
    '/no_think',
  ].join('\n');
}

// The user turn for one command: the screen's context as it is now, then the transcript word for word, last.
export function userTurn(transcript: Transcript, context: Record<string, unknown>): string {
  return `Screen context: ${JSON.stringify(context)}\nCommand: ${transcript.text}`;
}

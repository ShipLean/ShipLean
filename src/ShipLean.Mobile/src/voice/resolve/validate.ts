// A call checked against the screen's actions before the screen's spec sees it: the action must be one the spec
// lists, the phone must be able to take it, its note is copied word for word from the transcript, and its arguments
// must fit the action's schema (the subset the command grammar speaks: type, enum, const, properties, required, items).
import type {
  JsonSchema,
  ParsedCall,
  Transcript,
  VoiceActionSpec,
  VoiceFailure,
} from '../contracts';
import { noteText } from './note';

export type Checked =
  { ok: true; call: ParsedCall } | { ok: false; call: ParsedCall; failure: VoiceFailure };

const isObject = (v: unknown): v is Record<string, unknown> =>
  typeof v === 'object' && v !== null && !Array.isArray(v);

function hasType(value: unknown, type: unknown): boolean {
  switch (type) {
    case 'string':
      return typeof value === 'string';
    case 'integer':
      return typeof value === 'number' && Number.isInteger(value);
    case 'number':
      return typeof value === 'number' && Number.isFinite(value);
    case 'boolean':
      return typeof value === 'boolean';
    case 'null':
      return value === null;
    case 'array':
      return Array.isArray(value);
    case 'object':
      return isObject(value);
    default:
      return false;
  }
}

// Whether `value` fits `schema`. An object may hold only the properties its schema declares, as the grammar allows.
export function fits(value: unknown, schema: JsonSchema): boolean {
  if (Array.isArray(schema.enum))
    return schema.enum.some((e) => JSON.stringify(e) === JSON.stringify(value));
  if ('const' in schema) return JSON.stringify(schema.const) === JSON.stringify(value);
  if (schema.type !== undefined) {
    const types: unknown[] = Array.isArray(schema.type) ? schema.type : [schema.type];
    if (!types.some((t) => hasType(value, t))) return false;
  }
  if (Array.isArray(value) && isObject(schema.items)) {
    const items = schema.items;
    return value.every((v) => fits(v, items));
  }
  if (isObject(value) && (schema.type === 'object' || isObject(schema.properties))) {
    const properties = isObject(schema.properties) ? schema.properties : {};
    const required = Array.isArray(schema.required) ? schema.required : [];
    if (!required.every((r) => typeof r === 'string' && r in value)) return false;
    return Object.entries(value).every(
      ([name, v]) => isObject(properties[name]) && fits(v, properties[name] as JsonSchema),
    );
  }
  return true;
}

const rejected = (call: ParsedCall, failure: VoiceFailure): Checked => ({
  ok: false,
  call,
  failure,
});

export function checkCall(
  actions: VoiceActionSpec[],
  call: ParsedCall,
  transcript: Transcript,
): Checked {
  const action = actions.find((a) => a.name === call.action);
  if (action === undefined)
    return rejected(call, {
      kind: 'noValidAction',
      detail: { action: call.action, reason: 'none' },
    });
  if (action.webOnly !== undefined)
    return rejected(call, {
      kind: 'webOnly',
      detail: {
        action: action.name,
        where: action.webOnly,
        ...(action.webPage === undefined ? {} : { page: action.webPage }),
      },
    });
  let args = call.args;
  if (action.noteArg !== undefined) {
    // The model's own wording is never used: only the transcript's words at noteSpan.
    const note = call.noteSpan === undefined ? null : noteText(transcript, call.noteSpan);
    if (note === null)
      return rejected(call, { kind: 'unclear', detail: { heard: transcript.text } });
    args = { ...call.args, [action.noteArg]: note };
  }
  if (!isObject(args) || !fits(args, { type: 'object', ...action.args }))
    return rejected(call, {
      kind: 'noValidAction',
      detail: { action: action.name, reason: 'invalidArgs' },
    });
  return { ok: true, call: { ...call, args } };
}

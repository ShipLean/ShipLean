import type { RetroFixType } from '../../../api/retros';

/** The six types of fix an action item can have, in the order the dialog lists them, with the hint under each. */
export const retroFixOptions: { id: RetroFixType; label: string; hint: string }[] = [
  { id: 'skill', label: 'Skill', hint: 'A repeatable way to do a kind of task' },
  { id: 'guide', label: 'Guide', hint: 'The planning guide, a brief, or agent md' },
  { id: 'test', label: 'Test', hint: 'Automation that fails when it happens again' },
  { id: 'analyzer', label: 'Analyzer', hint: 'A static check that stops it at build time' },
  { id: 'product', label: 'Product', hint: 'Change ShipLean itself' },
  { id: 'other', label: 'Other', hint: 'None of these; say what in the description' },
];

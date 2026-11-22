import type { RetroFixType } from '@shiplean/core/api/retros';

/** The eight types of fix an action item can have, most deterministic first, with the hint under each. */
export const retroFixOptions: { id: RetroFixType; label: string; hint: string }[] = [
  {
    id: 'fix',
    label: 'Fix the broken thing',
    hint: 'Something is broken: a flaky test, a broken environment',
  },
  {
    id: 'structure',
    label: 'Structure',
    hint: 'Change types or code layout so the mistake cannot be written',
  },
  { id: 'analyzer', label: 'Analyzer', hint: 'A static check that stops it at build time' },
  { id: 'test', label: 'Test', hint: 'Automation that fails when it happens again' },
  { id: 'skill', label: 'Skill', hint: 'A repeatable way to do a kind of task' },
  { id: 'guide', label: 'Guide', hint: 'The planning guide, a brief, or agent md' },
  { id: 'product', label: 'Product', hint: 'Change ShipLean itself' },
  { id: 'other', label: 'Other', hint: 'None of these; say what in the description' },
];

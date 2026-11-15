import { useState } from 'react';
import type { VelocityDto } from '@shiplean/core/api/sprints';
import { useVelocityOverride } from './planningApi';

/** The velocity override being edited, shared by the desktop's inline editor and the phone's sheet. */
export function useVelocityDraft(velocity: VelocityDto, projectId: string, projectKey: string) {
  const planned = velocity.override ?? velocity.average;
  const [text, setText] = useState(String(planned));
  const save = useVelocityOverride(projectId, projectKey);
  const parsed = Number(text);
  return {
    planned,
    text,
    setText,
    save,
    parsed,
    valid: text.trim() !== '' && Number.isFinite(parsed) && parsed >= 0,
    submit: (value: number | null, onDone: () => void) => save.mutate(value, { onSuccess: onDone }),
  };
}

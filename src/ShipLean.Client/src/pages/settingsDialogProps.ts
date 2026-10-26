// What the boards and the Taskboard hand their settings dialog slot: the section is the query's ?settings=, choosing
// a section sets it (replacing the history entry), closing the dialog removes it (a new entry), every other query
// parameter is kept.
export type SetSearchParams = (next: URLSearchParams, options?: { replace?: boolean }) => void;

export interface SettingsDialogProps {
  section: string | undefined;
  onSectionChange(section: string): void;
  onClose(): void;
}

function withSettings(current: URLSearchParams, section: string | undefined) {
  const next = new URLSearchParams(current);
  if (section === undefined) next.delete('settings');
  else next.set('settings', section);
  return next;
}

export function settingsDialogProps(
  params: URLSearchParams,
  setParams: SetSearchParams,
): SettingsDialogProps {
  return {
    section: params.get('settings') ?? undefined,
    onSectionChange: (section) => setParams(withSettings(params, section), { replace: true }),
    onClose: () => setParams(withSettings(params, undefined)),
  };
}

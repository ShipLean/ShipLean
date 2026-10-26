import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { api } from '../../../api/client';
import { ToastHost } from '../../ToastHost';
import { SettingsDialog } from '../SettingsDialog';
import type { SaveOutcome } from '../types';
import { fakeGroups, type FakeDoc } from './fakePanes';
import { settingsHarnessIds as ids } from './harnessIds';

const FIRST: FakeDoc = { a: 'a0', b: 'b0', c: 'c0' };

interface PutResult {
  document: FakeDoc;
  mergedSections: string[];
}

/** CT-only: the frame over three one-input panes, saving through PUT /api/fake/settings (stubbed by the test). */
export function SettingsDialogHarness({ apply }: { apply: 'onSave' | 'immediately' }) {
  const [open, setOpen] = useState(true);
  const [document, setDocument] = useState(FIRST);
  const [params, setParams] = useSearchParams();

  const save = async (draft: FakeDoc, base: FakeDoc): Promise<SaveOutcome<FakeDoc>> => {
    const result = await api.put<PutResult>('/api/fake/settings', {
      basedOn: base,
      settings: draft,
    });
    setDocument(result.document);
    return { ok: true, ...result };
  };

  return (
    <>
      <ToastHost />
      {open ? (
        <SettingsDialog
          title="Fake settings"
          groups={fakeGroups}
          document={document}
          context={undefined}
          apply={apply}
          savedMessage="Saved for the test"
          section={params.get('settings') ?? undefined}
          defaultSection="a"
          onSectionChange={(id) => setParams({ settings: id }, { replace: true })}
          onSave={save}
          onClose={() => setOpen(false)}
        />
      ) : (
        <span data-testid={ids.closed} />
      )}
    </>
  );
}

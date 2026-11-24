import { useState } from 'react';
import { TabBand } from '../detail/TabBand';

type Id = 'source' | 'rendered' | 'history';

export function TabBandHarness({ onChanged }: { onChanged: (id: string) => void }) {
  const [active, setActive] = useState<Id>('source');
  return (
    <TabBand<Id>
      label="Prompt views"
      active={active}
      onChange={(id) => {
        setActive(id);
        onChanged(id);
      }}
      aside={<span data-testid="band-aside">Started from the built-in of 14 Nov</span>}
      tabs={[
        { id: 'source', label: 'Source', testId: 'band-source' },
        { id: 'rendered', label: 'Rendered', testId: 'band-rendered' },
        { id: 'history', label: 'History', count: 2, testId: 'band-history' },
      ]}
    />
  );
}

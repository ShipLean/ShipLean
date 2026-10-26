import { useState } from 'react';
import { useLocation } from 'react-router-dom';
import { BacklogToolbar, type BacklogToolbarProps } from '../toolbar/BacklogToolbar';

// CT-only: the toolbar over a little state, with the address shown so a test can see where the mode switch went.
export function ToolbarHarness({
  mode = 'list',
  withFilter = false,
  bare = false,
  withOptions = false,
}: {
  mode?: BacklogToolbarProps['mode'];
  withFilter?: boolean;
  // No Completed pair and no columns: the toolbar of a mode that has neither.
  bare?: boolean;
  withOptions?: boolean;
}) {
  const { pathname, search } = useLocation();
  const [completed, setCompleted] = useState(false);
  const [hidden, setHidden] = useState<string[]>([]);
  return (
    <>
      <BacklogToolbar
        projectKey="SHIP"
        level="stories"
        mode={mode}
        onNewItem={() => setHidden([...hidden, 'new-clicked'])}
        includeCompleted={bare ? undefined : completed}
        onIncludeCompletedChange={bare ? undefined : setCompleted}
        options={withOptions ? () => <span data-testid="harness-option">forecast</span> : undefined}
        columns={
          bare
            ? undefined
            : {
                options: [
                  { id: 'key', label: 'Key', required: true },
                  { id: 'state', label: 'State', required: false },
                ],
                hidden,
                onChange: setHidden,
              }
        }
        filter={
          withFilter ? () => <span data-testid="harness-filter">filter here</span> : undefined
        }
      />
      <span data-testid="harness-location">{pathname + search}</span>
      <span data-testid="harness-state">{`${completed}|${hidden.join(',')}`}</span>
    </>
  );
}

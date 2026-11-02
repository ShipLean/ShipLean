import { CommandHarness } from './CommandHarness';
import { BacklogSearchBox } from '../search/BacklogSearchBox';
import { useBacklogSearch } from '../search/useBacklogSearch';
import { SelectionProvider } from '../selection/SelectionProvider';

function Box() {
  const search = useBacklogSearch();
  return <BacklogSearchBox search={search} />;
}

// CT-only: the search box with CommandHarness's buttons, so a test runs the `search` command by name.
export function SearchHarness() {
  return (
    <CommandHarness>
      <SelectionProvider>
        <Box />
      </SelectionProvider>
    </CommandHarness>
  );
}

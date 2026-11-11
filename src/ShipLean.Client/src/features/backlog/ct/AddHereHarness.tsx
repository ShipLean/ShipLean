import { Fragment } from 'react';
import type { ProjectDto } from '@shiplean/core/api/projects';
import type { BacklogRowDto, BacklogSectionDto } from '@shiplean/core/api/views';
import { AddBelowProvider } from '../add/AddBelowProvider';
import { AddBelowRow } from '../add/AddBelowRow';
import { AddInButton, AddInSection } from '../add/AddInSection';
import type { SectionCollapse } from '../planning/useSectionCollapse';
import { useBacklog } from '../backlogApi';
import { SelectionProvider } from '../selection/SelectionProvider';
import { useRowGestures } from '../selection/useRowGestures';
import { useSelection } from '../selection/useSelection';
import { backlogTestIds as ids } from '../testIds';
import { CommandHarness } from './CommandHarness';

const COLUMNS = 3;
// Every section open, as the add row needs; the collapse itself is Sections.spec's.
const OPEN: SectionCollapse = {
  isOpen: () => true,
  toggle: () => undefined,
  open: () => undefined,
  searching: false,
  withSearch: () => OPEN,
};

function Row({
  row,
  filtering,
  project,
}: {
  row: BacklogRowDto;
  filtering: boolean;
  project: ProjectDto;
}) {
  const gestures = useRowGestures(row);
  return (
    <>
      <tr {...gestures} data-testid={ids.row} data-key={row.key}>
        <td colSpan={COLUMNS}>{row.title}</td>
      </tr>
      <AddBelowRow
        row={row}
        columnCount={COLUMNS}
        projectId={project.id}
        projectKey={project.key}
        filtering={filtering}
      />
    </>
  );
}

function Screen({
  project,
  mode,
  filtering,
}: {
  project: ProjectDto;
  mode: 'list' | 'planning';
  filtering: boolean;
}) {
  const view = useBacklog({
    projectId: project.id,
    level: 'stories',
    mode,
    filter: '',
    includeCompleted: false,
  }).data;
  const sections: BacklogSectionDto[] =
    view === undefined
      ? []
      : mode === 'planning'
        ? view.sections
        : [{ id: 'list', title: 'List', sprint: null, points: 0, capacity: null, rows: view.rows }];
  useSelection(sections.flatMap((section) => section.rows.map((row) => row.id)));
  return (
    <table>
      <tbody>
        {sections.map((section) => (
          <Fragment key={section.id}>
            {section.rows.map((row) => (
              <Row key={row.id} row={row} filtering={filtering} project={project} />
            ))}
            {section.sprint !== null ? (
              <>
                <tr>
                  <td colSpan={COLUMNS}>
                    <AddInButton section={section} filtering={filtering} collapse={OPEN} />
                  </td>
                </tr>
                <AddInSection section={section} columnCount={COLUMNS} projectId={project.id} />
              </>
            ) : null}
          </Fragment>
        ))}
      </tbody>
    </table>
  );
}

// CT-only: the rows of a list or of Planning's sections with their add-below rows and "+ Add" rows, under the list
// commands (CommandHarness's buttons run `addBelow`) and a selection. `filtering` stands for a filter being on.
export function AddHereHarness({
  project,
  mode = 'list',
  filtering = false,
}: {
  project: ProjectDto;
  mode?: 'list' | 'planning';
  filtering?: boolean;
}) {
  return (
    <CommandHarness>
      <SelectionProvider>
        <AddBelowProvider projectId={project.id} projectKey={project.key}>
          <Screen project={project} mode={mode} filtering={filtering} />
        </AddBelowProvider>
      </SelectionProvider>
    </CommandHarness>
  );
}

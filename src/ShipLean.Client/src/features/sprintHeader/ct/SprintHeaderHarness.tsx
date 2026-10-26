import { useLocation } from 'react-router-dom';
import { SprintHeader, type SprintHeaderProps } from '../SprintHeader';
import { sprintHeaderHarnessIds as harness } from './harnessIds';

export function SprintHeaderHarness(props: SprintHeaderProps) {
  const location = useLocation();
  return (
    <>
      <SprintHeader {...props} />
      <output data-testid={harness.location}>{location.pathname}</output>
    </>
  );
}

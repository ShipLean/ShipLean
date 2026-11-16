import { useNavigate } from 'react-router-dom';
import { AppRoutes } from '../../../AppRoutes';

/** The test id of the button AppRoutesWithGo draws. */
export const goTestId = 'ct-inspection-go';

// The real route table plus one button that moves the address to `to`, for component tests that change the address the
// way a view will (the placeholder views have no controls of their own).
export function AppRoutesWithGo({ to }: { to: string }) {
  const navigate = useNavigate();
  return (
    <>
      <AppRoutes />
      <button
        type="button"
        data-testid={goTestId}
        onClick={() => void navigate(to, { replace: true })}
      >
        go
      </button>
    </>
  );
}

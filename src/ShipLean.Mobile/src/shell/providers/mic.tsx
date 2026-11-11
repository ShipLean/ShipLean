import { ToastHost } from '../../ui/Toast';
import { MicPermissionWatcher } from '../../voice/mic/MicPermissionWatcher';
import type { AppSlotProps } from './types';

// The microphone permission, watched app-wide. It sits above every project's ToastHost, so it has its own for the
// toast that says the microphone is back.
export function MicProvider({ children }: AppSlotProps) {
  return (
    <ToastHost>
      <MicPermissionWatcher />
      {children}
    </ToastHost>
  );
}

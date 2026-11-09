import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  deviceSessionKeys,
  listDeviceSessions,
  revokeDeviceSession,
  type DeviceSessionDto,
} from '@shiplean/core/api/deviceSessions';
import { showToast } from '../../components/toastStore';

export function useDeviceSessions() {
  return useQuery({ queryKey: deviceSessionKeys.list, queryFn: () => listDeviceSessions() });
}

// Ends one phone's session. Whatever the outcome the list is read again, so it shows what the server holds.
export function useSignOutDevice(onDone: () => void) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (device: DeviceSessionDto) => revokeDeviceSession(device.id),
    onSuccess: (_result, device) => showToast(`${device.name} is signed out.`),
    onSettled: () => {
      onDone();
      return queryClient.invalidateQueries({ queryKey: deviceSessionKeys.list });
    },
  });
}

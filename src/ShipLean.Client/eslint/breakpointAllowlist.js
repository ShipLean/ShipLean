// Existing uses of banned breakpoint variants (shiplean/no-breakpoint-variants, K9), counted per file under
// src/ShipLean.Client/src/, grouped by the task or lane that converts them to touch:/phone:. A file must have exactly
// its count: converting a use means lowering the count, and deleting the line once it reaches zero. Never add a line.
export const breakpointAllowlist = [
  // SHIP-713 (L713b, wave 1): quick search
  // W01-O2: the settings field panes
  // SHIP-726: sprint capacity
  { file: 'features/sprints/capacity/CapacityHeader.tsx', count: 1, owner: 'SHIP-726' },
  // SHIP-733 (L733a): the profile pages' max-sm:
  { file: 'features/settings/profile/identity/FormRow.tsx', count: 2, owner: 'SHIP-733' },
  { file: 'features/settings/profile/identity/IdentityForm.tsx', count: 1, owner: 'SHIP-733' },
  { file: 'features/settings/profile/account/PasswordPanel.tsx', count: 1, owner: 'SHIP-733' },
  { file: 'features/settings/profile/account/AppearancePanel.tsx', count: 1, owner: 'SHIP-733' },
  // SHIP-707: stays, instance settings are excluded from mobile
  { file: 'features/settings/instance/SignInRules.tsx', count: 1, owner: 'SHIP-707' },
];

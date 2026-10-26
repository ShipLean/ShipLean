import { expect, test } from 'vitest';
import { shortcutLabel } from './shortcutLabel';

test('shortcutLabel_WhenMac_ShouldBeCommandP', () => {
  expect(shortcutLabel({ userAgentData: { platform: 'macOS' } })).toBe('⌘P');
  expect(shortcutLabel({ platform: 'MacIntel' })).toBe('⌘P');
});

test('shortcutLabel_WhenNotMac_ShouldBeCtrlP', () => {
  expect(shortcutLabel({ platform: 'Win32' })).toBe('Ctrl+P');
  expect(shortcutLabel({})).toBe('Ctrl+P');
});

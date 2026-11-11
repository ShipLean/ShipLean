import { describe, expect, it } from 'vitest';
import { agentDisplayName } from './agentName';

describe('agentDisplayName', () => {
  it('drops the client in brackets', () => {
    expect(agentDisplayName('Claude Code (claude.ai)')).toBe('Claude Code');
  });
  it('drops the unverified mark', () => {
    expect(agentDisplayName('Cursor (unverified)')).toBe('Cursor');
  });
  it('leaves a plain name unchanged', () => {
    expect(agentDisplayName('Claude Code')).toBe('Claude Code');
  });
});

import { describe, expect, it } from 'vitest';
import { agentDisplayName } from './agentName';

describe('agentDisplayName', () => {
  it('drops the client in brackets', () => {
    expect(agentDisplayName('Claude Code (claude.ai)')).toBe('Claude Code');
  });
  it('drops the unverified mark', () => {
    expect(agentDisplayName('Codex (unverified)')).toBe('Codex');
  });
  it('leaves a plain name unchanged', () => {
    expect(agentDisplayName('Claude Code')).toBe('Claude Code');
  });
});

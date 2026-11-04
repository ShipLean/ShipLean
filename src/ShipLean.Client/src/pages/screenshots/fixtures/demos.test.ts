import { describe, expect, test } from 'vitest';
import { demoStates, settingsStates } from './demos';

// The page shots must never bless impossible data. These are the status and YouTube rules of K4 and K6 that the pinned
// JSON is held to by DemoFixturesObeyContractTests, applied to every state the shots show, derived ones included.

const LIVE_SESSIONS = ['submitted', 'working', 'inputRequired'];
const states = Object.entries(demoStates);

describe('demo states', () => {
  test.each(states)('%s ties its sprint id to its name', (_name, demo) => {
    expect(demo.sprintName).toBe(demo.sprintId === 'sprint-24' ? 'Sprint 24' : 'Sprint 23');
  });

  test.each(states.filter(([, demo]) => demo.status === 'ready'))(
    '%s is ready with a video, chapters and a script',
    (_name, demo) => {
      expect(demo.video).not.toBeNull();
      expect(demo.scriptHtml).not.toBeNull();
      expect(demo.chapters.length).toBeGreaterThan(0);
      expect(demo.video!.contentUrl).toBe(`/api/attachments/${demo.video!.attachmentId}/content`);
    },
  );

  test.each(states.filter(([, demo]) => ['closable', 'none', 'blocked'].includes(demo.status)))(
    '%s has no run, video, chapters or script',
    (_name, demo) => {
      expect(demo.run).toBeNull();
      expect(demo.video).toBeNull();
      expect(demo.chapters).toEqual([]);
      expect(demo.scriptHtml).toBeNull();
    },
  );

  test('closable cannot be created and says to close the sprint', () => {
    expect(demoStates.closable.actions.canCreate).toBe(false);
    expect(demoStates.closable.statusMessage).toBe('Close the sprint to make its demo.');
  });

  test.each(states.filter(([, demo]) => demo.status === 'working'))(
    '%s has a live session and cannot be created',
    (_name, demo) => {
      expect(LIVE_SESSIONS).toContain(demo.run!.sessionState);
      expect(demo.actions.canCreate).toBe(false);
    },
  );

  test.each(states.filter(([, demo]) => demo.status === 'failed'))(
    '%s has no live session (a live one would be working) and says why',
    (_name, demo) => {
      expect(LIVE_SESSIONS).not.toContain(demo.run?.sessionState);
      expect(demo.statusMessage).not.toBeNull();
    },
  );

  test('requested has a run and no session yet', () => {
    expect(demoStates.requested.run).not.toBeNull();
    expect(demoStates.requested.run!.sessionState).toBeNull();
  });

  test.each(states)('%s has an embed only when published and not private', (_name, demo) => {
    const { state, embedUrl, privacy } = demo.youTube;
    expect(embedUrl !== null).toBe(state === 'published' && privacy !== 'private');
    if (embedUrl !== null) {
      expect(embedUrl.startsWith('https://www.youtube-nocookie.com/embed/')).toBe(true);
    }
  });

  test.each(states)(
    '%s allows a YouTube retry only after a failure with a video',
    (_name, demo) => {
      expect(demo.actions.canRetryYouTube).toBe(
        demo.youTube.state === 'failed' && demo.video !== null,
      );
    },
  );
});

describe('settings states', () => {
  test('a refusal belongs to a channel that is not connected yet', () => {
    expect(settingsStates.refused.youTube.refusal).not.toBeNull();
    expect(settingsStates.refused.youTube.status).toBe('pending');
  });

  test('a connected channel has a visibility and a checked label', () => {
    expect(settingsStates.branded.youTube.status).toBe('connected');
    expect(settingsStates.branded.youTube.visibility).not.toBeNull();
    expect(settingsStates.branded.youTube.checkedLabel).not.toBeNull();
  });

  test('a channel that is not connected has no title', () => {
    expect(settingsStates.notConnected.youTube.channelTitle).toBeNull();
  });
});

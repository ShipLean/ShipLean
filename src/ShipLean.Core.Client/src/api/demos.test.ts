import { expect, test } from 'vitest';
import none from '../../../ShipLean.TestSupport/DtoFixtures/demos/sprint-demo-none.json';
import working from '../../../ShipLean.TestSupport/DtoFixtures/demos/sprint-demo-working.json';
import ready from '../../../ShipLean.TestSupport/DtoFixtures/demos/sprint-demo-ready.json';
import unlisted from '../../../ShipLean.TestSupport/DtoFixtures/demos/sprint-demo-youtube-unlisted.json';
import priv from '../../../ShipLean.TestSupport/DtoFixtures/demos/sprint-demo-youtube-private.json';
import failed from '../../../ShipLean.TestSupport/DtoFixtures/demos/sprint-demo-youtube-failed.json';
import settings from '../../../ShipLean.TestSupport/DtoFixtures/demos/project-demo-settings.json';
import branded from '../../../ShipLean.TestSupport/DtoFixtures/demos/project-demo-settings-branded.json';
import brand from '../../../ShipLean.TestSupport/DtoFixtures/demos/demo-brand.json';
import brandDefault from '../../../ShipLean.TestSupport/DtoFixtures/demos/demo-brand-default.json';
import brandFallback from '../../../ShipLean.TestSupport/DtoFixtures/demos/demo-brand-logo-fallback.json';
import chapters from '../../../ShipLean.TestSupport/DtoFixtures/demos/demo-chapters.json';
import disconnect from '../../../ShipLean.TestSupport/DtoFixtures/demos/youtube-disconnect.json';
import {
  SPRINT_DEMO_STATUSES,
  YOU_TUBE_DEMO_STATES,
  YOU_TUBE_VISIBILITIES,
  YOU_TUBE_CONNECTION_STATUSES,
  type DemoBrand,
  type DemoChaptersJson,
  type DemoUploadedDto,
  type ProjectDemoSettingsDto,
  type SprintDemoDto,
  type YouTubeDisconnectDto,
} from './demos';

// Typecheck fails if the fixture lacks a field, has one of the wrong type, or has one the type doesn't declare.
type DeepWiden<T> = T extends string
  ? string
  : T extends number
    ? number
    : T extends boolean
      ? boolean
      : T extends readonly (infer U)[]
        ? DeepWiden<U>[]
        : T extends object
          ? { [K in keyof T]: DeepWiden<T[K]> }
          : T;

type NoExtraKeys<U, T> = U extends readonly (infer UE)[]
  ? T extends readonly (infer TE)[]
    ? NoExtraKeys<UE, TE>[]
    : U
  : U extends object
    ? T extends object
      ? { [K in keyof U]: K extends keyof T ? NoExtraKeys<U[K], T[K]> : never }
      : U
    : U;

function typed<T>() {
  return <U extends DeepWiden<T>>(value: U & NoExtraKeys<U, DeepWiden<T>>): DeepWiden<T> => value;
}

test('demoFixtures_ShouldTypeEverySprintDemo', () => {
  const demos = [
    typed<SprintDemoDto>()(none),
    typed<SprintDemoDto>()(working),
    typed<SprintDemoDto>()(ready),
    typed<SprintDemoDto>()(unlisted),
    typed<SprintDemoDto>()(priv),
    typed<SprintDemoDto>()(failed),
  ];
  for (const demo of demos) {
    expect(SPRINT_DEMO_STATUSES).toContain(demo.status);
    expect(YOU_TUBE_DEMO_STATES).toContain(demo.youTube.state);
  }
  expect(ready.video.contentUrl).toBe(`/api/attachments/${ready.video.attachmentId}/content`);
  expect(priv.youTube.privacy).toBe('private');
  expect(YOU_TUBE_VISIBILITIES).toContain(unlisted.youTube.privacy);
  expect(failed.youTube.state).toBe('failed');
});

test('demoFixtures_ShouldTypeSettingsBrandAndTheRest', () => {
  for (const file of [settings, branded]) {
    const typedSettings = typed<ProjectDemoSettingsDto>()(file);
    expect(YOU_TUBE_CONNECTION_STATUSES).toContain(typedSettings.youTube.status);
    expect(typedSettings.brand.defaultTokens).toHaveLength(7);
  }
  expect(branded.brand.logoUrl).not.toBeNull();
  for (const file of [brand, brandDefault, brandFallback]) typed<DemoBrand>()(file);
  expect(brandDefault.logoIsDefault).toBe(true);
  expect(brandFallback.logoFallbackReason).not.toBeNull();
  typed<DemoChaptersJson>()(chapters);
  typed<YouTubeDisconnectDto>()(disconnect);
});

// No pinned file for the upload answer: the literal is the K4 record's fields, typed the same way.
test('demoUploaded_ShouldTypeTheUploadAnswer', () => {
  const uploaded = typed<DemoUploadedDto>()({
    sprintName: 'Sprint 24',
    videoAttachmentId: ready.video.attachmentId,
    videoBytes: ready.video.sizeBytes,
    chapters: ready.chapters.length,
    openUrl: '/p/SHIP/items/SHIP-200',
  });
  expect(uploaded.chapters).toBe(2);
});

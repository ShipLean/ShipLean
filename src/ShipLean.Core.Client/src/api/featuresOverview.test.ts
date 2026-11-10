import { afterEach, describe, expect, it, vi } from 'vitest';
import digest from '../../../ShipLean.TestSupport/DtoFixtures/screens/feature-digest.json';
import overview from '../../../ShipLean.TestSupport/DtoFixtures/screens/features-overview.json';
import {
  DIGEST_LINE_KINDS,
  featuresKeys,
  getFeatureDigest,
  getFeaturesOverview,
  markDigestSeen,
  type FeatureDigestDto,
  type FeaturesOverviewDto,
} from './featuresOverview';

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

function stubFetch(response: Response) {
  const fetchStub = vi.fn().mockResolvedValue(response);
  vi.stubGlobal('fetch', fetchStub);
  return fetchStub;
}

function sent(fetchStub: ReturnType<typeof stubFetch>) {
  const [url, init] = fetchStub.mock.calls[0] as [string, RequestInit];
  return {
    url,
    method: init.method,
    body: init.body === undefined ? undefined : (JSON.parse(String(init.body)) as unknown),
  };
}

const ok = (body: unknown = {}, status = 200) => new Response(JSON.stringify(body), { status });
const noContent = () => new Response(null, { status: 204 });

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('featuresOverview', () => {
  it('types features-overview.json and feature-digest.json with no extra keys', () => {
    typed<FeaturesOverviewDto>()(overview);
    typed<FeatureDigestDto>()(digest);
    const kinds = [...digest.needsYou, ...digest.changed].flatMap((i) =>
      i.lines.map((l) => l.kind),
    );
    for (const kind of kinds) expect(DIGEST_LINE_KINDS as readonly string[]).toContain(kind);
  });

  it('getFeaturesOverview GETs the overview route', async () => {
    const stub = stubFetch(ok(overview));
    expect(await getFeaturesOverview('p/1')).toEqual(overview);
    expect(sent(stub)).toEqual({
      url: '/api/projects/p%2F1/features/overview',
      method: 'GET',
      body: undefined,
    });
  });

  it('getFeatureDigest adds the cursor only when given', async () => {
    const first = stubFetch(ok(digest));
    expect(await getFeatureDigest('p')).toEqual(digest);
    expect(sent(first).url).toBe('/api/projects/p/features/digest');
    const next = stubFetch(ok(digest));
    await getFeatureDigest('p', 'a|b c');
    expect(sent(next)).toEqual({
      url: '/api/projects/p/features/digest?cursor=a%7Cb%20c',
      method: 'GET',
      body: undefined,
    });
  });

  it('markDigestSeen POSTs the seen route with no body', async () => {
    const stub = stubFetch(noContent());
    await expect(markDigestSeen('p/1')).resolves.toBeUndefined();
    expect(sent(stub)).toEqual({
      url: '/api/projects/p%2F1/features/digest/seen',
      method: 'POST',
      body: undefined,
    });
  });

  it('featuresKeys share one root so a seen invalidates the overview and the digest', () => {
    const root = featuresKeys.root('p');
    expect(root).toEqual(['projects', 'p', 'features']);
    expect(featuresKeys.overview('p').slice(0, 3)).toEqual(root);
    expect(featuresKeys.digest('p', null).slice(0, 3)).toEqual(root);
    expect(featuresKeys.digest('p', 'c')).not.toEqual(featuresKeys.digest('p', null));
  });
});

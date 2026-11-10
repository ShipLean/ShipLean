import { api, ApiRequestError } from '@shiplean/core/api/client';
import { afterEach, describe, expect, it } from 'vitest';
import { controlledApi } from './controlledApi';
import { restoreFetch } from './fakeApi';

afterEach(restoreFetch);

describe('controlledApi', () => {
  it('answers the table, and a failNext reply once', async () => {
    const fake = controlledApi({ 'GET /api/me': { name: 'Tom' } });
    fake.failNext('GET /api/me', { status: 500, body: { error: 'Boom', code: 'internal_error' } });

    await expect(api.get('/api/me')).rejects.toMatchObject({ status: 500 });
    await expect(api.get('/api/me')).resolves.toEqual({ name: 'Tom' });
    expect(fake.callsTo('GET /api/me')).toHaveLength(2);
  });

  it('rejects every request with a TypeError while offline', async () => {
    const fake = controlledApi({ 'GET /api/me': { name: 'Tom' } });
    fake.offline(true);
    await expect(api.get('/api/me')).rejects.toBeInstanceOf(TypeError);
    await expect(api.get('/api/elsewhere')).rejects.toBeInstanceOf(TypeError);

    fake.offline(false);
    await expect(api.get('/api/me')).resolves.toEqual({ name: 'Tom' });
  });

  it('holds a hung route until it is released', async () => {
    const fake = controlledApi({ 'POST /api/x': { status: 204 } });
    fake.hang('POST /api/x');
    let settled = false;
    const sent = api.post('/api/x').then(() => (settled = true));
    for (let i = 0; i < 20; i++) await Promise.resolve();
    expect(settled).toBe(false);
    expect(fake.callsTo('POST /api/x')).toHaveLength(1);

    fake.release('POST /api/x');
    await sent;
    expect(settled).toBe(true);
  });

  it('answers an unlisted route 404, also after a hang is released', async () => {
    const fake = controlledApi({});
    const error = await api.get('/api/nope').catch((e: unknown) => e);
    expect(error).toBeInstanceOf(ApiRequestError);
    expect(error).toMatchObject({ status: 404, code: 'not_found' });

    fake.hang('GET /api/later');
    const later = api.get('/api/later').catch((e: unknown) => e);
    fake.release('GET /api/later');
    expect(await later).toMatchObject({ status: 404, code: 'not_found' });
  });
});

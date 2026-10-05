import { error, type RequestEvent } from '@sveltejs/kit';
import { env } from '$env/dynamic/private';
import { clearSession, writeSession } from './session';

/** Base URL of the internal .NET API (never exposed to browsers). */
export const apiUrl = () => env.API_URL ?? 'http://localhost:8080';

const UPSTREAM_COOKIE = 'einsatz';

/** Extracts the API's auth cookie ("einsatz=…") from a Set-Cookie response. */
export function upstreamCookie(res: Response): string | null {
  for (const c of res.headers.getSetCookie()) if (c.startsWith(`${UPSTREAM_COOKIE}=`)) return c.split(';')[0]!;
  return null;
}

/**
 * Calls the API on behalf of the signed-in user. Refreshed upstream cookies (sliding expiration)
 * are folded back into the BFF session; an upstream 401 ends the BFF session too.
 */
export async function upstream(event: RequestEvent, path: string, init: RequestInit = {}): Promise<Response> {
  if (!event.locals.upstream || !event.locals.user) error(401, 'Nicht angemeldet.');
  const headers = new Headers(init.headers);
  headers.set('cookie', event.locals.upstream);
  headers.set('x-forwarded-for', event.getClientAddress());

  const res = await fetch(`${apiUrl()}${path}`, { ...init, headers });

  const renewed = upstreamCookie(res);
  if (renewed && renewed !== event.locals.upstream) {
    event.locals.upstream = renewed;
    writeSession(event.cookies, { user: event.locals.user, upstream: renewed }, event.url.protocol === 'https:');
  }
  if (res.status === 401) {
    clearSession(event.cookies);
    error(401, 'Sitzung abgelaufen.');
  }
  return res;
}

/** GraphQL is an internal contract between BFF and API; the browser only ever sees view models. */
export async function gql<T>(event: RequestEvent, query: string, variables?: Record<string, unknown>): Promise<T> {
  const res = await upstream(event, '/graphql', {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ query, variables }),
  });
  const body = (await res.json()) as { data?: T; errors?: { message: string; extensions?: { code?: string } }[] };
  if (body.errors?.length) {
    const businessRule = body.errors.every((e) => e.extensions?.code === 'BUSINESS_RULE');
    error(businessRule ? 422 : 502, body.errors.map((e) => e.message).join(' '));
  }
  return body.data as T;
}

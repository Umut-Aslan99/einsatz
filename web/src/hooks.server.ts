import type { Handle } from '@sveltejs/kit';
import { SESSION_COOKIE, unseal } from '$lib/server/session';

export const handle: Handle = async ({ event, resolve }) => {
  const session = unseal(event.cookies.get(SESSION_COOKIE));
  event.locals.user = session?.user ?? null;
  event.locals.upstream = session?.upstream ?? null;

  // SvelteKit already blocks cross-site form posts; JSON-only writes cover the BFF's fetch endpoints too.
  const { method, headers } = event.request;
  if (event.url.pathname.startsWith('/api/') && !['GET', 'HEAD'].includes(method)
      && !headers.get('content-type')?.startsWith('application/json')) {
    return new Response('JSON required', { status: 415 });
  }

  const response = await resolve(event);
  response.headers.set('Referrer-Policy', 'strict-origin-when-cross-origin');
  response.headers.set('X-Content-Type-Options', 'nosniff');
  response.headers.set('Permissions-Policy', 'geolocation=(), microphone=()');
  return response;
};

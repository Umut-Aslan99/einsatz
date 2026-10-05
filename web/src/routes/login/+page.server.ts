import { fail, redirect } from '@sveltejs/kit';
import { apiUrl, upstreamCookie } from '$lib/server/upstream';
import { writeSession } from '$lib/server/session';
import type { User } from '$lib/types';

export const load = ({ locals }) => {
  if (locals.user) redirect(303, locals.user.role === 'Office' ? '/buero' : '/t');
};

export const actions = {
  /** The BFF logs in upstream and keeps the API cookie inside its own encrypted session. */
  default: async ({ request, cookies, url, getClientAddress }) => {
    const form = await request.formData();
    const login = String(form.get('login') ?? '').trim();
    const password = String(form.get('password') ?? '');
    if (!login || !password) return fail(400, { login, error: 'Bitte Benutzername und Passwort angeben.' });

    let res: Response;
    try {
      res = await fetch(`${apiUrl()}/api/auth/login`, {
        method: 'POST',
        headers: { 'content-type': 'application/json', 'x-forwarded-for': getClientAddress() },
        body: JSON.stringify({ login, password }),
      });
    } catch {
      return fail(503, { login, error: 'Der Server ist gerade nicht erreichbar.' });
    }
    if (res.status === 429) return fail(429, { login, error: 'Zu viele Versuche. Bitte eine Minute warten.' });
    const upstream = upstreamCookie(res);
    if (!res.ok || !upstream) return fail(401, { login, error: 'Benutzername oder Passwort ist falsch.' });

    const user = (await res.json()) as User;
    writeSession(cookies, { user, upstream }, url.protocol === 'https:');
    redirect(303, user.role === 'Office' ? '/buero' : '/t');
  },
};

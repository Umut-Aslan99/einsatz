import { createCipheriv, createDecipheriv, createHash, randomBytes } from 'node:crypto';
import { env } from '$env/dynamic/private';
import type { Cookies } from '@sveltejs/kit';
import type { User } from '$lib/types';

/**
 * BFF session: an AES-256-GCM encrypted, HttpOnly cookie holding the user and the upstream API credential.
 * Stateless (survives restarts, no session store), tamper-proof (GCM auth tag), unreadable for JavaScript.
 */
export const SESSION_COOKIE = 'einsatz_session';
const MAX_AGE = 60 * 60 * 24 * 14;

export interface Session { user: User; upstream: string }

function key(): Buffer {
  const secret = env.SESSION_SECRET;
  if (!secret || secret.length < 32) throw new Error('SESSION_SECRET must be set (≥ 32 characters).');
  return createHash('sha256').update(secret).digest();
}

export function seal(session: Session): string {
  const iv = randomBytes(12);
  const cipher = createCipheriv('aes-256-gcm', key(), iv);
  const body = Buffer.concat([cipher.update(JSON.stringify(session), 'utf8'), cipher.final()]);
  return Buffer.concat([iv, cipher.getAuthTag(), body]).toString('base64url');
}

export function unseal(value: string | undefined): Session | null {
  if (!value) return null;
  try {
    const raw = Buffer.from(value, 'base64url');
    const decipher = createDecipheriv('aes-256-gcm', key(), raw.subarray(0, 12));
    decipher.setAuthTag(raw.subarray(12, 28));
    const json = Buffer.concat([decipher.update(raw.subarray(28)), decipher.final()]).toString('utf8');
    return JSON.parse(json) as Session;
  } catch {
    return null; // tampered, expired key or garbage: treat as signed out
  }
}

export function writeSession(cookies: Cookies, session: Session, secure: boolean) {
  cookies.set(SESSION_COOKIE, seal(session), { path: '/', httpOnly: true, sameSite: 'strict', secure, maxAge: MAX_AGE });
}

export function clearSession(cookies: Cookies) {
  cookies.delete(SESSION_COOKIE, { path: '/' });
}

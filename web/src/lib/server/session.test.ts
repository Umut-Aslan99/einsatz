import { beforeAll, describe, expect, it, vi } from 'vitest';

vi.mock('$env/dynamic/private', () => ({ env: { SESSION_SECRET: 'x'.repeat(40) } }));
let seal: typeof import('./session').seal;
let unseal: typeof import('./session').unseal;
beforeAll(async () => ({ seal, unseal } = await import('./session')));

const session = { user: { id: 'u1', login: 'jonas', name: 'Jonas', role: 'Technician' as const }, upstream: 'einsatz=abc' };

describe('BFF session cookie', () => {
  it('round-trips user and upstream credential', () => {
    expect(unseal(seal(session))).toEqual(session);
  });

  it('never contains the upstream credential in readable form', () => {
    const sealed = seal(session);
    expect(sealed).not.toContain('einsatz');
    expect(Buffer.from(sealed, 'base64url').toString('latin1')).not.toContain('jonas');
  });

  it('uses a fresh IV per cookie', () => {
    expect(seal(session)).not.toBe(seal(session));
  });

  it('rejects tampered or foreign cookies', () => {
    const raw = Buffer.from(seal(session), 'base64url');
    raw[raw.length - 1]! ^= 1;
    expect(unseal(raw.toString('base64url'))).toBeNull();
    expect(unseal('not-a-session')).toBeNull();
    expect(unseal(undefined)).toBeNull();
  });
});

import { api, OfflineError } from '$lib/api';
import * as store from '$lib/offline/db';
import type { User } from '$lib/types';

/** Technician-side session. Online it asks the BFF; offline it trusts the last user stored on the device. */
class Session {
  user = $state<User | null>(null);
  ready = $state(false);

  async load() {
    try {
      const user = await api<User>('/api/session');
      const previous = await store.meta.get<User>('user');
      if (previous && previous.id !== user.id) await store.clearAll(); // another person on this device
      await store.meta.set('user', user);
      this.user = user;
    } catch (e) {
      this.user = e instanceof OfflineError ? ((await store.meta.get<User>('user')) ?? null) : null;
    }
    this.ready = true;
  }

  async logout() {
    await store.clearAll();
    await fetch('/logout', { method: 'POST', headers: { accept: 'application/json' } }).catch(() => {});
    this.user = null;
  }
}

export const session = new Session();

import { api, ApiError, OfflineError } from '$lib/api';
import { apply } from '$lib/offline/apply';
import * as store from '$lib/offline/db';
import type { Article, Command, CommandType, Job, PayloadOf, User } from '$lib/types';

const MAX_BATCH_BYTES = 4_000_000;
const SYNC_INTERVAL_MS = 20_000;

interface PushResult { id: string; outcome: 'applied' | 'duplicate' | 'rejected'; error?: string }

/**
 * The technician's offline-first state. Every change is recorded as a command in an IndexedDB
 * outbox and applied locally right away; a background loop pushes the outbox and pulls a fresh
 * snapshot whenever there is a connection. Nothing is lost when the app is closed offline.
 */
class FieldStore {
  user = $state<User | null>(null);
  // Raw (non-proxied) state: these arrays are always replaced as a whole, and the reducer
  // structuredClones jobs, which deep $state proxies would not allow.
  serverJobs = $state.raw<Job[]>([]);
  outbox = $state.raw<store.OutboxEntry[]>([]);
  articles = $state.raw<Article[]>([]);
  online = $state(typeof navigator === 'undefined' ? true : navigator.onLine);
  syncing = $state(false);
  lastSync = $state<string | null>(null);
  rejected = $state.raw<{ command: Command; error: string }[]>([]);
  sessionExpired = $state(false);

  /** Server snapshot + not-yet-confirmed local changes = what the technician sees. */
  jobs = $derived.by(() => {
    const ctx = { articles: this.articles, localUrls: {} };
    return this.serverJobs.map((job) =>
      this.outbox.filter((e) => e.command.jobId === job.id).reduce((j, e) => apply(j, e.command, ctx), job));
  });

  pendingCount = $derived(this.outbox.length);

  #timer: ReturnType<typeof setInterval> | undefined;
  #debounce: ReturnType<typeof setTimeout> | undefined;

  async start(user: User) {
    this.user = user;
    const [jobs, outbox, articles, lastSync] = await Promise.all([
      store.allJobs(), store.pending(), store.meta.get<Article[]>('articles'), store.meta.get<string>('lastSync'),
    ]);
    this.serverJobs = jobs;
    this.outbox = outbox.filter((e) => e.userId === user.id);
    this.articles = articles ?? [];
    this.lastSync = lastSync ?? null;

    if (!this.#timer) {
      addEventListener('online', this.#onOnline);
      addEventListener('offline', this.#onOffline);
      document.addEventListener('visibilitychange', this.#onVisible);
      this.#timer = setInterval(() => this.sync(), SYNC_INTERVAL_MS);
    }
    await this.sync();
  }

  stop() {
    clearInterval(this.#timer);
    this.#timer = undefined;
    removeEventListener('online', this.#onOnline);
    removeEventListener('offline', this.#onOffline);
    document.removeEventListener('visibilitychange', this.#onVisible);
  }

  job(id: string) {
    return this.jobs.find((j) => j.id === id);
  }

  /** Records a change. Works offline; the UI updates immediately through `jobs`. */
  async record<T extends CommandType>(jobId: string, type: T, payload?: PayloadOf<T>) {
    if (!this.user) return;
    const command = { id: crypto.randomUUID(), jobId, type, at: new Date().toISOString(), payload } as Command;
    await store.enqueue({ userId: this.user.id, command });
    this.outbox = (await store.pending()).filter((e) => e.userId === this.user!.id);
    clearTimeout(this.#debounce);
    this.#debounce = setTimeout(() => this.sync(), 400);
  }

  dismissRejected(id: string) {
    this.rejected = this.rejected.filter((r) => r.command.id !== id);
  }

  async sync() {
    if (this.syncing || !this.user || this.sessionExpired) return;
    this.syncing = true;
    try {
      await this.#push();
      await this.#pull();
      this.online = true;
    } catch (e) {
      if (e instanceof OfflineError) this.online = false;
      else if (e instanceof ApiError && e.status === 401) this.sessionExpired = true;
      else console.warn('[sync]', e);
    } finally {
      this.syncing = false;
    }
  }

  async #push() {
    while (true) {
      const queue = (await store.pending()).filter((e) => e.userId === this.user!.id);
      if (queue.length === 0) break;

      // Photos are large: split into batches that stay well below the request limit.
      const batch: store.OutboxEntry[] = [];
      let bytes = 0;
      for (const e of queue) {
        const size = JSON.stringify(e.command).length;
        if (batch.length > 0 && (bytes + size > MAX_BATCH_BYTES || batch.length >= 100)) break;
        batch.push(e);
        bytes += size;
      }

      const { results } = await api<{ results: PushResult[] }>('/api/sync/push', { body: { commands: batch.map((e) => e.command) } });
      const byId = new Map(results.map((r) => [r.id, r]));
      for (const e of batch) {
        const r = byId.get(e.command.id);
        if (r?.outcome === 'rejected') this.rejected = [...this.rejected, { command: e.command, error: r.error ?? 'Abgelehnt' }];
      }
      await store.removeFromOutbox(batch.filter((e) => byId.has(e.command.id)).map((e) => e.seq!));
      this.outbox = (await store.pending()).filter((e) => e.userId === this.user!.id);
    }
  }

  async #pull() {
    const res = await api<{ serverTime: string; jobs: Job[]; articles: Article[] }>('/api/sync/pull');
    await store.replaceJobs(res.jobs);
    await store.meta.set('articles', res.articles);
    await store.meta.set('lastSync', res.serverTime);
    this.serverJobs = res.jobs;
    this.articles = res.articles;
    this.lastSync = res.serverTime;
  }

  #onOnline = () => { this.online = true; this.sync(); };
  #onOffline = () => { this.online = false; };
  #onVisible = () => { if (document.visibilityState === 'visible') this.sync(); };
}

export const field = new FieldStore();

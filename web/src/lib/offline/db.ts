import { openDB, type DBSchema, type IDBPDatabase } from 'idb';
import type { Article, Command, Job, User } from '$lib/types';

export interface OutboxEntry {
  seq?: number;
  userId: string;
  command: Command;
}

interface Schema extends DBSchema {
  /** Last server snapshot of each job. */
  jobs: { key: string; value: Job };
  /** Commands not yet confirmed by the server, in recording order. */
  outbox: { key: number; value: OutboxEntry; indexes: { byJob: string } };
  meta: { key: string; value: unknown };
}

let dbp: Promise<IDBPDatabase<Schema>> | undefined;

export function db() {
  dbp ??= openDB<Schema>('einsatz', 1, {
    upgrade(d) {
      d.createObjectStore('jobs', { keyPath: 'id' });
      const outbox = d.createObjectStore('outbox', { keyPath: 'seq', autoIncrement: true });
      outbox.createIndex('byJob', 'command.jobId');
      d.createObjectStore('meta');
    },
  });
  return dbp;
}

export const meta = {
  async get<T>(key: 'user' | 'articles' | 'lastSync'): Promise<T | undefined> {
    return (await (await db()).get('meta', key)) as T | undefined;
  },
  async set(key: 'user' | 'articles' | 'lastSync', value: User | Article[] | string | undefined) {
    const d = await db();
    if (value === undefined) await d.delete('meta', key);
    else await d.put('meta', value, key);
  },
};

export async function replaceJobs(jobs: Job[]) {
  const tx = (await db()).transaction('jobs', 'readwrite');
  await tx.store.clear();
  await Promise.all(jobs.map((j) => tx.store.put(j)));
  await tx.done;
}

export async function allJobs(): Promise<Job[]> {
  return (await db()).getAll('jobs');
}

export async function enqueue(entry: OutboxEntry) {
  await (await db()).add('outbox', entry);
}

export async function pending(): Promise<OutboxEntry[]> {
  return (await db()).getAll('outbox');
}

export async function removeFromOutbox(seqs: number[]) {
  const tx = (await db()).transaction('outbox', 'readwrite');
  await Promise.all(seqs.map((s) => tx.store.delete(s)));
  await tx.done;
}

/** Wipes everything device-local (logout). */
export async function clearAll() {
  const d = await db();
  await Promise.all([d.clear('jobs'), d.clear('outbox'), d.clear('meta')]);
}

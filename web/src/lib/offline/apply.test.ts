import { describe, expect, it } from 'vitest';
import { apply, canComplete } from './apply';
import type { Article, Command, Job } from '$lib/types';

const articles: Article[] = [{ number: 'A-10010', name: 'Hydrauliköl', unit: 'l', price: 6.9 }];
const ctx = { articles, localUrls: {} };
let n = 0;
const cmd = (type: Command['type'], payload?: unknown, at = '2026-10-05T08:00:00Z') =>
  ({ id: `c${++n}`, jobId: 'j1', type, at, payload }) as Command;

const job = (): Job => ({
  id: 'j1', number: 'A-260141', scheduledFor: '2026-10-05', task: 'Wartung', status: 'Planned', notes: null,
  customer: { number: 'K1', name: 'Kunde', street: 'Weg 1', city: 'Bocholt' }, machine: { name: 'Presse', serialNumber: 'S1' },
  technicianName: 'Jonas', timeEntries: [], materials: [], photos: [], signature: null, completedAt: null, approvedAt: null,
  approvedBy: null, returnReason: null, integrations: [], totalMinutes: 0, materialTotal: 0, reportUrl: '',
});

const run = (...cmds: Command[]) => cmds.reduce((j, c) => apply(j, c, ctx), job());

describe('local command replay', () => {
  it('tracks time and moves the job in progress', () => {
    const j = run(cmd('startWork'), cmd('stopWork', undefined, '2026-10-05T09:30:00Z'));
    expect(j.status).toBe('InProgress');
    expect(j.totalMinutes).toBe(90);
  });

  it('adds material once per line id with price snapshot', () => {
    const add = cmd('addMaterial', { lineId: 'l1', articleNumber: 'A-10010', quantity: 3 });
    const j = run(add, add);
    expect(j.materials).toHaveLength(1);
    expect(j.materialTotal).toBe(20.7);
  });

  it('only completes when the server would accept it', () => {
    const unsigned = run(cmd('startWork'), cmd('stopWork'), cmd('complete'));
    expect(unsigned.status).toBe('InProgress');
    expect(canComplete(unsigned).missing).toEqual(['Unterschrift des Kunden']);

    const done = run(cmd('startWork'), cmd('stopWork'), cmd('sign', { signedBy: 'M. Kunde', pngBase64: 'x' }), cmd('complete'));
    expect(done.status).toBe('Completed');
  });

  it('freezes a completed job', () => {
    const done = run(cmd('startWork'), cmd('stopWork'), cmd('sign', { signedBy: 'K', pngBase64: 'x' }), cmd('complete'));
    const after = apply(done, cmd('setNotes', { text: 'zu spät' }), ctx);
    expect(after.notes).toBeNull();
  });

  it('never mutates the server snapshot it starts from', () => {
    const base = job();
    apply(base, cmd('startWork'), ctx);
    expect(base.timeEntries).toHaveLength(0);
  });
});

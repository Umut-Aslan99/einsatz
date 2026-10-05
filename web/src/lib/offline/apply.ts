import type { Article, Command, Job } from '$lib/types';

/**
 * Applies a pending command to the local copy of a job, so the UI reflects changes instantly
 * even offline. Mirrors the server rules (Einsatz.Domain/Job.cs); the server stays the authority
 * and may still reject a command, which the sync engine then reports.
 */
export function apply(job: Job, cmd: Command, ctx: { articles: Article[]; localUrls: Record<string, string> }): Job {
  const j: Job = structuredClone(job);
  const editable = j.status === 'Planned' || j.status === 'InProgress';
  if (!editable) return j;
  const touch = () => { if (j.status === 'Planned') j.status = 'InProgress'; };

  switch (cmd.type) {
    case 'startWork':
      if (j.timeEntries.some((t) => t.end === null)) return j;
      j.timeEntries.push({ id: cmd.id, start: cmd.at, end: null });
      j.status = 'InProgress';
      break;
    case 'stopWork': {
      const open = j.timeEntries.find((t) => t.end === null);
      if (open) open.end = cmd.at;
      break;
    }
    case 'setNotes':
      j.notes = cmd.payload.text?.trim() || null;
      touch();
      break;
    case 'addMaterial': {
      const p = cmd.payload;
      const a = ctx.articles.find((x) => x.number === p.articleNumber);
      if (!a || p.quantity <= 0 || j.materials.some((m) => m.id === p.lineId)) return j;
      j.materials.push({
        id: p.lineId, articleNumber: a.number, articleName: a.name, unit: a.unit,
        quantity: p.quantity, unitPrice: a.price, total: round2(p.quantity * a.price),
      });
      touch();
      break;
    }
    case 'removeMaterial':
      j.materials = j.materials.filter((m) => m.id !== cmd.payload.lineId);
      break;
    case 'addPhoto':
      if (j.photos.some((p) => p.id === cmd.payload.photoId)) return j;
      j.photos.push({
        id: cmd.payload.photoId, caption: cmd.payload.caption, takenAt: cmd.at,
        url: ctx.localUrls[cmd.payload.photoId] ?? `data:${cmd.payload.contentType};base64,${cmd.payload.base64}`,
      });
      touch();
      break;
    case 'sign':
      j.signature = { signedBy: cmd.payload.signedBy.trim(), signedAt: cmd.at };
      touch();
      break;
    case 'complete':
      if (!canComplete(j).ok) return j;
      j.status = 'Completed';
      j.completedAt = cmd.at;
      j.returnReason = null;
      break;
  }
  return recompute(j);
}

/** The same preconditions the server checks, phrased for the technician. */
export function canComplete(j: Job): { ok: boolean; missing: string[] } {
  const missing: string[] = [];
  if (j.timeEntries.length === 0) missing.push('Arbeitszeit erfassen');
  if (j.timeEntries.some((t) => t.end === null)) missing.push('Laufende Zeit stoppen');
  if (!j.signature) missing.push('Unterschrift des Kunden');
  return { ok: missing.length === 0, missing };
}

export function minutes(t: { start: string; end: string | null }, now = Date.now()): number {
  const end = t.end ? Date.parse(t.end) : now;
  return Math.max(0, Math.round((end - Date.parse(t.start)) / 60000));
}

function recompute(j: Job): Job {
  j.totalMinutes = j.timeEntries.filter((t) => t.end).reduce((n, t) => n + minutes(t), 0);
  j.materialTotal = round2(j.materials.reduce((n, m) => n + m.total, 0));
  return j;
}

const round2 = (n: number) => Math.round(n * 100) / 100;

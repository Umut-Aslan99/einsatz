// Mirrors the API read models (Einsatz.Api/Jobs/JobDto.cs).

export type Role = 'Technician' | 'Office';
export type JobStatus = 'Planned' | 'InProgress' | 'Completed' | 'Approved' | 'Exported';

export interface User { id: string; login: string; name: string; role: Role }
export interface Article { number: string; name: string; unit: string; price: number }
export interface TimeEntry { id: string; start: string; end: string | null }
export interface Material { id: string; articleNumber: string; articleName: string; unit: string; quantity: number; unitPrice: number; total: number }
export interface Photo { id: string; caption: string | null; takenAt: string; url: string }
export interface Integration { id: string; target: 'ERP' | 'DMS'; fileName: string; createdAt: string; url: string }

export interface Job {
  id: string;
  number: string;
  scheduledFor: string;
  task: string;
  status: JobStatus;
  notes: string | null;
  customer: { number: string; name: string; street: string; city: string };
  machine: { name: string; serialNumber: string };
  technicianName: string;
  timeEntries: TimeEntry[];
  materials: Material[];
  photos: Photo[];
  signature: { signedBy: string; signedAt: string } | null;
  completedAt: string | null;
  approvedAt: string | null;
  approvedBy: string | null;
  returnReason: string | null;
  integrations: Integration[];
  totalMinutes: number;
  materialTotal: number;
  reportUrl: string;
}

/** A change made on the device. Stored in the outbox until the server confirms it. */
export type Command =
  | Base<'startWork'> | Base<'stopWork'> | Base<'complete'>
  | Base<'setNotes', { text: string | null }>
  | Base<'addMaterial', { lineId: string; articleNumber: string; quantity: number }>
  | Base<'removeMaterial', { lineId: string }>
  | Base<'addPhoto', { photoId: string; contentType: string; base64: string; caption: string | null }>
  | Base<'sign', { signedBy: string; pngBase64: string }>;

interface Base<T extends string, P = undefined> {
  id: string;
  jobId: string;
  type: T;
  at: string;
  payload: P;
}

export type CommandType = Command['type'];
export type PayloadOf<T extends CommandType> = Extract<Command, { type: T }>['payload'];

export const statusLabel: Record<JobStatus, string> = {
  Planned: 'Geplant',
  InProgress: 'In Arbeit',
  Completed: 'Zur Prüfung',
  Approved: 'Freigegeben',
  Exported: 'Im ERP',
};

import type { RequestEvent } from '@sveltejs/kit';
import type { Job, JobStatus } from '$lib/types';
import { gql, upstream } from './upstream';

// BFF view models for the office. The browser receives exactly what each screen renders.

const fromGql: Record<string, JobStatus> = {
  PLANNED: 'Planned', IN_PROGRESS: 'InProgress', COMPLETED: 'Completed', APPROVED: 'Approved', EXPORTED: 'Exported',
};

export interface OfficeRow {
  id: string; number: string; scheduledFor: string; status: JobStatus;
  customer: string; machine: string; technician: string; totalMinutes: number; materialTotal: number;
}

export async function listRows(event: RequestEvent): Promise<OfficeRow[]> {
  const d = await gql<{ jobs: (Omit<Job, 'customer' | 'machine'> & { customer: { name: string }; machine: { name: string } })[] }>(event, `{
    jobs { id number scheduledFor status technicianName totalMinutes materialTotal customer { name } machine { name } } }`);
  return d.jobs.map((j) => ({
    id: j.id, number: j.number, scheduledFor: j.scheduledFor, status: fromGql[j.status] ?? j.status,
    customer: j.customer.name, machine: j.machine.name, technician: j.technicianName,
    totalMinutes: j.totalMinutes, materialTotal: j.materialTotal,
  }));
}

const DETAIL = `
  id number scheduledFor task status notes technicianName
  customer { number name street city } machine { name serialNumber }
  timeEntries { id start end }
  materials { id articleNumber articleName unit quantity unitPrice total }
  photos { id caption takenAt url } signature { signedBy signedAt }
  completedAt approvedAt approvedBy returnReason
  integrations { id target fileName createdAt url }
  totalMinutes materialTotal reportUrl`;

export interface OfficeDetail { job: Job; erpXml: string | null }

/** One round trip for the page: the job via GraphQL plus the ERP file content, aggregated in the BFF. */
export async function detail(event: RequestEvent, id: string): Promise<OfficeDetail | null> {
  const d = await gql<{ job: Job | null }>(event, `query($id: UUID!) { job(id: $id) { ${DETAIL} } }`, { id });
  if (!d.job) return null;
  const job = { ...d.job, status: fromGql[d.job.status] ?? d.job.status };
  const erp = job.integrations.find((i) => i.target === 'ERP');
  const erpXml = erp ? await (await upstream(event, erp.url)).text() : null;
  return { job, erpXml };
}

export async function approve(event: RequestEvent, id: string) {
  await gql(event, `mutation($id: UUID!) { approveJob(id: $id) { id } }`, { id });
}

export async function returnToTechnician(event: RequestEvent, id: string, reason: string) {
  await gql(event, `mutation($id: UUID!, $reason: String!) { returnJob(id: $id, reason: $reason) { id } }`, { id, reason });
}

export async function resetDemo(event: RequestEvent) {
  await gql(event, `mutation { resetDemo }`);
}

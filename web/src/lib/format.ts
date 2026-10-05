const de = 'de-DE';
export const fmtDate = (iso: string) => new Date(iso).toLocaleDateString(de, { weekday: 'short', day: '2-digit', month: '2-digit' });
export const fmtTime = (iso: string) => new Date(iso).toLocaleTimeString(de, { hour: '2-digit', minute: '2-digit' });
export const fmtDateTime = (iso: string) => `${new Date(iso).toLocaleDateString(de)} ${fmtTime(iso)}`;
export const fmtMoney = (n: number) => n.toLocaleString(de, { style: 'currency', currency: 'EUR' });
export const fmtDuration = (min: number) => `${Math.floor(min / 60)}:${String(Math.round(min % 60)).padStart(2, '0')} h`;
export const todayIso = () => new Date().toLocaleDateString('sv-SE'); // yyyy-mm-dd in local time

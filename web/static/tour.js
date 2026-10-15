// Guided tour of Einsatz – texts here, mechanics in site-tour.js (shared by all demos of umut-aslan.dev).
// Starts on the sign-in page; after choosing a role it shows the technician app or the back office.
import { startTours } from '/site-tour.js';

const at = (prefix) => () => location.pathname.startsWith(prefix);
const technician = () => /^\/t(\/|$)/.test(location.pathname);
const office = at('/buero');

startTours([{
  id: 'einsatz',
  steps: [
    {
      title: 'Hallo! Kurz zum Problem',
      mood: 'worried',
      text: `Servicetechniker schreiben Einsatzberichte **auf Papier** oder tippen sie abends nach. In der Halle des Kunden gibt es
oft **keinen Empfang**.

Im Büro wird dann alles **ins ERP abgetippt** – Tage später, mit Lesefehlern und fehlenden Unterschriften.`,
      nextLabel: 'Zeig mir die Lösung',
    },
    {
      when: at('/login'),
      target: '[data-tour="personas"]',
      interactive: true,
      advanceOnClick: true,
      title: 'Wählen Sie eine Rolle',
      text: `**Techniker** arbeiten auf dem Tablet beim Kunden, der **Innendienst** prüft im Büro.

Am besten starten Sie als **Jonas (Techniker)** – den Innendienst können Sie danach ansehen.`,
    },
    {
      when: technician,
      path: '/t',
      target: '[data-tour="jobs"]',
      title: 'Meine Einsätze',
      text: `Heute, liegengeblieben, demnächst – die Aufträge sind **auf dem Tablet gespeichert** und auch ohne Netz da.`,
    },
    {
      when: technician,
      target: '[data-tour="sync"]',
      title: 'Kein Empfang? Kein Problem',
      mood: 'curious',
      text: `Ohne Netz wird jede Eingabe **auf dem Gerät gespeichert** und später synchronisiert – doppelte Übertragung ausgeschlossen.
Hier sieht man, ob noch etwas wartet.`,
    },
    {
      when: technician,
      path: '/t',
      click: '[data-tour="jobs"] a',
      target: '[data-tour="time"]',
      title: 'Arbeitszeit per Knopfdruck',
      text: `Start und Stopp – die Zeit läuft auch offline mit. Kein Nachtragen am Abend.`,
    },
    {
      when: technician,
      target: '[data-tour="material"]',
      title: 'Material aus dem Artikelstamm',
      text: `Verbautes Material mit Menge auswählen – Preise kommen aus dem Artikelstamm, nichts wird abgeschrieben.`,
    },
    {
      when: technician,
      target: '[data-tour="photos"]',
      title: 'Fotos direkt vom Tablet',
      text: `Vorher, nachher, Typenschild: Fotos werden verkleinert und offline zwischengespeichert, bis wieder Netz da ist.`,
    },
    {
      when: technician,
      target: '[data-tour="signature"]',
      title: 'Unterschrift beim Kunden',
      text: `Der Kunde unterschreibt **auf dem Bildschirm**. Danach wird der Einsatz abgeschlossen und geht an den Innendienst.`,
    },
    {
      when: office,
      path: '/buero',
      target: '[data-tour="office-kpis"]',
      title: 'Der Innendienst',
      text: `Was ist unterwegs, was wartet auf Prüfung – auf einen Blick.`,
    },
    {
      when: office,
      target: '[data-tour="reports"]',
      title: 'Berichte prüfen',
      text: `Alle Einsätze mit Status. Abgeschlossene Berichte kommen vollständig an: Zeiten, Material, Fotos, Unterschrift.`,
    },
    {
      when: () => office() && document.querySelector('[data-tour="review-row"]') !== null,
      click: '[data-tour="review-row"] a',
      target: '[data-tour="review"]',
      title: 'Freigeben – und fertig',
      text: `Ein Klick erzeugt das **PDF**, legt es im **DMS** ab und übergibt Arbeitszeit und Material als **XML-Beleg ans ERP**.
Fehlt etwas, geht der Bericht mit Begründung zurück an den Techniker.`,
    },
    {
      title: 'Warum das hilft',
      mood: 'celebrate',
      text: `- **Keine Zettel**, kein Abtippen – der Bericht entsteht beim Kunden
- funktioniert **ohne Netz**, synchronisiert sicher nach
- Beleg **direkt im ERP**, unterschriebenes PDF **im DMS**

Gebaut mit SvelteKit (Frontend und Backend-for-Frontend), ASP.NET Core, GraphQL und PostgreSQL.
Über den runden Knopf unten links starten Sie diese Tour jederzeit neu – probieren Sie auch die andere Rolle.`,
      nextLabel: 'Selbst ausprobieren',
    },
  ],
}]);

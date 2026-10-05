# Wie KI in diesem Projekt eingesetzt wurde

Dieses Projekt ist mit einem KI-Coding-Agenten (Claude Code) entstanden. Ich verstehe KI als schnellen
Mitentwickler, nicht als Autopilot. Die Arbeitsweise:

1. **Anforderungen und Architektur zuerst.** Problem (Servicebericht vor Ort, ERP-Übergabe), Rollen,
   Offline-Anforderung und Schichtenmodell (Frontend · BFF · Backend) wurden vorgegeben, nicht generiert.
2. **Kleine, prüfbare Schritte.** Domäne → API → Tests → Frontend → Container → End-to-End.
3. **Tests als Abnahme.** Jede Behauptung im README ist durch einen Test belegt. Die Tests haben echte
   Fehler gefunden, u. a.:
   - EF Core behandelte neue Zeiteinträge mit gesetzter ID als bestehend → `UPDATE` statt `INSERT`;
   - Zeitzonenfehler, der nur außerhalb von UTC auftritt (ADR 0005);
   - Svelte-5-Proxies ließen sich nicht klonen → Offline-Vorschau blieb stehen;
   - relative Asset-Pfade brachen den Offline-Neustart unter `/t/<id>`.
4. **Strenge Werkzeuge.** `TreatWarningsAsErrors`, .NET-Analyzer auf „latest-recommended“,
   `svelte-check --fail-on-warnings`, TypeScript strict.
5. **Review.** Jede Änderung wird gelesen und verstanden, bevor sie bleibt. Was ich nicht erklären kann,
   kommt nicht ins Repository.

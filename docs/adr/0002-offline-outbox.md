# 0002 · Offline per Befehls-Outbox statt Datensynchronisation

**Status:** angenommen

## Kontext
In Hallen und Kellern gibt es oft kein Netz. Techniker dürfen nie warten oder Daten verlieren.

## Entscheidung
Jede Änderung wird als Befehl (`startWork`, `addMaterial`, `sign`, …) mit gerätegenerierter ID in einer
IndexedDB-Outbox gespeichert und sofort lokal angewendet. Ein Hintergrundprozess sendet die Outbox, sobald
Netz da ist, und lädt danach einen frischen Stand. Der Server merkt sich jede Befehls-ID (`ProcessedCommands`),
dadurch ist erneutes Senden nach Verbindungsabbruch harmlos. Ein Regelverstoß verwirft nur diesen Befehl
und wird dem Techniker angezeigt.

## Konsequenzen
+ Keine Konfliktauflösung auf Feldebene nötig – die Domänenregeln entscheiden.
+ Die Absicht bleibt erhalten („Material hinzugefügt“ statt „Liste überschrieben“).
− Die Regeln existieren zweimal (C#-Domäne, TS-Reducer für die Vorschau); der Server bleibt maßgeblich,
  Tests sichern beide Seiten.

# 0001 · SvelteKit als Backend-for-Frontend

**Status:** angenommen

## Kontext
Zwei sehr unterschiedliche Clients: eine offlinefähige Techniker-App und eine Büroanwendung. Die .NET-API
soll nicht für jede Bildschirmmaske eigene Endpunkte bekommen, und Zugangsdaten sollen nicht im Browser liegen.

## Entscheidung
Der SvelteKit-Server ist der einzige öffentliche Einstiegspunkt (BFF):
- verwaltet die Browser-Session selbst (AES-256-GCM-verschlüsseltes, HttpOnly-, SameSite=Strict-Cookie)
  und hält das Auth-Cookie der API ausschließlich serverseitig;
- rendert die Innendienst-Seiten serverseitig und führt Workflow-Schritte als Form Actions aus
  (funktioniert ohne JavaScript, CSRF-Prüfung durch SvelteKit);
- spricht intern GraphQL mit der API und liefert dem Browser nur View-Models;
- reicht für die Techniker-App Sync-Befehle und Dateien durch und kürzt Antworten auf das Nötige.

## Konsequenzen
+ API und GraphQL-Schema sind nicht öffentlich – kleinere Angriffsfläche.
+ Frontend-Änderungen brauchen selten API-Änderungen.
− Ein zusätzlicher Hop und ein weiterer Dienst im Betrieb (Node-Container, ~60 MB RAM).

# 0005 · Zeitstempel zentral in UTC speichern

**Status:** angenommen

## Kontext
Npgsql speichert `timestamptz` nur mit Offset 0. Server (Europe/Berlin) und Geräte liefern `+02:00`.
Die Tests liefen zunächst in UTC-Containern und waren grün – erst der Start in der Produktions-Zeitzone
scheiterte.

## Entscheidung
Ein EF-Konverter normalisiert jeden `DateTimeOffset` beim Speichern auf UTC (`AppDb.ConfigureConventions`).
Die Backend-Tests laufen dauerhaft mit `TZ=Europe/Berlin`; ohne den Konverter schlagen dort 7 von 17 Tests fehl.

# 0003 · REST für die Synchronisation, GraphQL für den Innendienst

**Status:** angenommen

- **Sync (REST):** zwei feste Endpunkte (`pull`, `push`), große Nutzlasten (Fotos), einfach zu cachen und zu
  wiederholen. GraphQL brächte hier keinen Vorteil.
- **Innendienst (GraphQL, Hot Chocolate):** Listen und Detailansichten brauchen unterschiedliche Felder;
  der BFF fragt genau das ab, was eine Seite braucht. Geschäftsfehler kommen als `BUSINESS_RULE`
  mit verständlicher Meldung.
- Lese-Modelle sind EF-Projektionen: Bilddaten werden nie in Listen geladen.

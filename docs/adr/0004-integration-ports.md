# 0004 · ERP und DMS hinter Ports

**Status:** angenommen

Die Anwendung kennt nur `IErpConnector` und `IDocumentArchive`. Die Demo nutzt dateibasierte Adapter
(XML-Beleg in einen Importordner, PDF plus Index-JSON in eine Ablage) – der verbreitetste Integrationsweg
im Mittelstand. Ein Adapter für die Web-API eines konkreten ERP oder DMS ersetzt nur diese Klassen.

Freigabe ist atomar: PDF, ERP-Export und Ablage gelingen gemeinsam oder der Bericht bleibt „zur Prüfung“.
Ein nicht lesbares Foto wird im PDF als Platzhalter dargestellt und blockiert die Freigabe nicht.

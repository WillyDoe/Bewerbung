# tei – Technische Entwickler-Dokumentation (Onboarding)

Dieses Dokument richtet sich an **Software-Ingenieurinnen und -Ingenieure** sowie an **KI-gestützte Agenten**, die sich technisch in das tei-Projekt (MVS-Phase) einarbeiten. Es fasst Spezifikationen, Architektur, Konventionen und Review-Kriterien zusammen.

**Kontext:** Minimal Viable Solution (MVS), Fokus `tei_penServiceConnectionManager` und WPF-UI `tei_penService_ui`.

---

## UI-Projekt (tei_penService_ui) ausführen

So startest du die WPF-Oberfläche **tei_penService_ui** auf deinem Gerät:

### Voraussetzungen

- **Windows** (10 oder 11)
- **.NET Framework 4.6.2** (in der Regel unter Windows 10/11 bereits vorhanden)
- **Visual Studio 2022** (oder 2019) mit Workload „.NET-Desktopentwicklung“ – empfohlen für Build und Debug

### Mit Visual Studio (empfohlen)

1. **Solution öffnen**  
   Im Projektordner `TEI_APP.sln` doppelklicken oder in Visual Studio **Datei → Öffnen → Projekt/Projektmappe** und `TEI_APP.sln` auswählen.

2. **Startprojekt setzen**  
   Im Projektmappen-Explorer **tei_penService_ui** mit Rechtsklick auswählen → **Als Startprojekt festlegen**.

3. **Starten**  
   **F5** (mit Debugging) oder **Strg+F5** (ohne Debugging). Die tei Pen Service UI startet als Fenster auf dem Desktop.

### Von der Kommandozeile (PowerShell / cmd)

Aus dem Stammordner der Solution (dort, wo `TEI_APP.sln` liegt):

```powershell
# Mit MSBuild (Developer Command Prompt / Visual Studio Developer PowerShell):
msbuild TEI_APP.sln /p:Configuration=Debug /t:tei_penService_ui
.\tei_penService_ui\bin\Debug\tei_penService_ui.exe
```

Oder nur das UI-Projekt bauen und starten:

```powershell
msbuild tei_penService_ui\tei_penService_ui.csproj /p:Configuration=Debug
.\tei_penService_ui\bin\Debug\tei_penService_ui.exe
```

**Hinweis:** Für Bluetooth und Neosmartpen-Funktionen muss das Neosmartpen Windows SDK (Ordner `Windows-SDK2.0-master`) im Repository vorhanden sein; die UI referenziert die DLL daraus.

---

## Technische Spezifikationen tei-AI-Begleiter (Exploring Kit Vision)

- **Hardware**: Kompaktes AI-Mini-Tablet mit integriertem Computing-System
- **Betriebssystem**: Windows 10/11 (oder später Linux/Android)
- **Framework**: .NET Framework 4.6.2 (für SDK-Kompatibilität)
- **Verbindung**: Bluetooth Low Energy (BLE) für tei-flow (aktuell Neosmartpen)
- **Datenformat**: JSON (UTF-8) – AI-freundliches Format mit vollständigen Metadaten (ausschließlich für MVS-Zwecke, später Custom-Datei Format)
- **Speicherung**: Lokale JSON-Dateien + optionale Cloud-Synchronisation (ausschließlich für MVS-Zwecke, später Custom-Datei Format)
- **Netzwerk**: Verbindung zur Schul-Cloud für Lerninhalte & Compute
- **Offline-Funktionalität**: Vollständig offline-fähig (Daten bleiben lokal)
- **Integration**: Nahtlose Verbindung mit Mini-Kamera, Bluetooth-Kopfhörern und tei-flow

---

## Coding Standards

- **PascalCase** für public methods, properties und classes
- **camelCase** für private fields und local variables
- **async/await** Pattern mit try-catch blocks
- **DRY** (Don't Repeat Yourself) Prinzipien
- **Nullable reference types** (`<Nullable>enable</Nullable>`)
- **Implicit usings** (`<ImplicitUsings>enable</ImplicitUsings>`)
- **Framework-spezifische Bibliotheken**: Prüfe zuerst verfügbare .NET Framework Bibliotheken für gewünschte Features (Hinweis: Das Projekt verwendet .NET Framework 4.6.2, nicht .NET MAUI)

---

## AI-First Design

Datenformate und Metadaten so gestalten, dass AI sie leicht verarbeiten kann.

Keine unstrukturierten Rohblöcke ohne Metadaten.

---

## Erweiterbarkeit

Architektur so gestalten, dass später ein Extension Store wie bei VSCode möglich ist: modulare Erweiterungen für verschiedene Funktionen, für welche die Schülerinnen und Schüler eigenständige Features entwickeln können.

---

## Technische Spezifikationen tei_penServiceConnectionManager

### Architektur des tei_penServiceConnectionManager

**MVS-Phase (Aktuell):**

```
┌─────────────────────────────────────────────────────────────┐
│         Schüler-PC/Laptop (Standard-Hardware)               │
│                                                             │
│  ┌─────────────────────────────────────────────────────────────┐ │
│  │  tei_penServiceUI (WPF-Anwendung)                           │ │
│  │  - Benutzeroberfläche für Schüler                           │ │
│  │  - Geräteverwaltung & Verbindungsstatus                     │ │
│  │  - Datenvisualisierung                                      │ │
│  └─────────────────────────────────────────────────────────────┘ │
│           ↕ In-Process (Hybrid Wrapping)                         │
│  ┌─────────────────────────────────────────────────────────────┐ │
│  │  ServiceOrchestrator (Service-Management)                   │ │
│  │  - Orchestriert alle Services                               │ │
│  │  - Isolierte Exception-Handling                             │ │
│  └─────────────────────────────────────────────────────────────┘ │
│           ↕                      ↕                               │
│  ┌────────────────────────────┐  ┌─────────────────────────────┐ │
│  │ tei_penService             │  │ tei_penServiceDataTransfer  │ │
│  │ ConnectionManager          │  │ (zukünftig)                 │ │
│  │                            │  │                             │ │
│  │ - 1:1 Stift-               │  │ - Datenübertragung          │ │
│  │   verbindung               │  │ - Datenverarbeitung         │ │
│  │ - Bluetooth                │  │ - Batch-Verarbeitung        │ │
│  │   Management               │  │ - JSON-Serialisierung       │ │
│  └────────────────────────────┘  └─────────────────────────────┘ │
│           ↓                      ↓                               │
│  ┌─────────────────────────────────────────────────────────────┐ │
│  │  Lokale Dateispeicherung (Schüler-PC)                       │ │
│  │  - JSON-Dateien (Section_Owner_Note/Page_*.json)            │ │
│  │  - Alle Stiftdaten lokal gespeichert                        │ │
│  │  - UTF-8 Encoding (AI-freundlich)                           │ │
│  └─────────────────────────────────────────────────────────────┘ │
│                          ↓                                  │
│              HTTP/HTTPS (LAN - lokales Netzwerk)            │
│                          ↓                                  │
└─────────────────────────────────────────────────────────────┘
                          │
                          │
┌─────────────────────────────────────────────────────────────┐
│              Lehrer-Laptop/Desktop                          │
│                                                             │
│  ┌─────────────────────────────────────────────────────────────┐
│  │  tei_teacherHub (zukünftig)                               │ │
│  │                                                           │ │
│  │  Datenempfang:                                            │ │
│  │  - Netzwerk-Server für Schüler-Daten                      │ │
│  │  - HTTP/HTTPS Endpoint                                    │ │
│  │  - Automatischer Datenempfang                             │ │
│  │  - Datenvalidierung & Speicherung                         │ │
│  │                                                           │ │
│  │  Aufgaben-Erstellung:                                     │ │
│  │  - Generative KI für Aufgaben-Generierung                 │ │
│  │    (ChatGPT API Integration)                              │ │
│  │  - NCode-Papier Modul                                     │ │
│  │  - Aufgaben-Export (PDF + NCode)                          │ │
│  │                                                           │ │
│  │  KI-Auswertung:                                           │ │
│  │  - Automatische Auswertung handschriftlicher              │ │
│  │    Schülerantworten                                       │ │
│  │  - ChatGPT API Integration                                │ │
│  │  - Modell-Auswahl (GPT-4o / GPT-4o-mini)                  │ │
│  │  - Feedback-Generierung                                   │ │
│  │  - Lernstand-Dokumentation                                │ │
│  └─────────────────────────────────────────────────────────────┘│
│                          ↓                                      │
│  ┌─────────────────────────────────────────────────────────────┐│
│  │  OpenAI ChatGPT API (Cloud-basiert)                       │ │
│  │  - GPT-4o (Standard für komplexe Auswertungen)            │ │
│  │  - GPT-4o-mini (für einfache Aufgaben, kostengünstig)     │ │
│  │  - Vision API (für handschriftliche Erkennung)            │ │
│  │  - (Später: Lokale Fine-tuned Modelle möglich)            │ │
│  └─────────────────────────────────────────────────────────────┘
└─────────────────────────────────────────────────────────────┘
```

### Technische Details

**Schüler-PC Komponenten:**

- **tei_penServiceUI**: WPF-Anwendung (.NET Framework 4.6.2) für Benutzeroberfläche
- **tei_penServiceConnectionManager**: Service für Bluetooth Pen-Verbindungen (1:1)
- **tei_penServiceDataTransfer**: Service für Datenübertragung und -verarbeitung (zukünftig)
- **ServiceOrchestrator**: Zentrale Orchestrierung mit Hybrid Wrapping für Fehlerisolation
- **Lokale Speicherung**: JSON-Dateien auf Schüler-PC (keine Cloud-Synchronisation im MVS)
- **Netzwerk-Übertragung**: HTTP/HTTPS Client für Übertragung der Daten an Lehrer-PC über lokales Netzwerk (LAN)

**Lehrer-PC Komponenten (zukünftig):**

- **tei_teacherHub**: Desktop-Anwendung für Aufgaben-Erstellung und Auswertung
- **Netzwerk-Server**: HTTP/HTTPS Server-Endpoint zum Empfang von Schüler-Daten über lokales Netzwerk
- **NCode-Papier Modul**: Integration des Neosmartpen NCode-Services
- **ChatGPT API Integration**: Automatische Korrektur und Feedback-Generierung
  - **GPT-4o**: Standard für komplexe Auswertungen und Argumentationsbewertung
  - **GPT-4o-mini**: Für einfache Aufgaben (Multiple Choice, Kurzantworten) – kostengünstig
  - **Vision API**: Für direkte handschriftliche Erkennung aus Scans/Bildern

### SDK & Bibliotheken

- **SDK**: Neosmartpen Windows SDK 2.0 (.NET Framework 4.6.2)
- **JSON-Bibliothek**: Newtonsoft.Json (für .NET Framework)
- **KI-API**: OpenAI .NET SDK für ChatGPT-Integration
- **Netzwerk-Kommunikation**: System.Net.Http für HTTP/HTTPS Client & Server
- **Thread-Safety**: Alle Connection-Operationen müssen thread-safe sein
- **Resource Management**: IDisposable Pattern für alle Ressourcen
- **Event-Handling**: SDK-Events für asynchrone Datenübertragung
- **Error Handling**: Umfassendes Exception-Handling für Bluetooth-Operationen

### Kommunikationsarchitektur

- **In-Process**: Alle Services auf Schüler-PC laufen im selben Prozess (In-Process mit Hybrid Wrapping)
- **Schüler → Lehrer Übertragung**: HTTP/HTTPS über lokales Netzwerk (LAN)
  - **Client-Side** (Schüler-PC): tei_penServiceDataTransfer sendet JSON-Dateien via HTTP POST
  - **Server-Side** (Lehrer-PC): tei_teacherHub empfängt und speichert Daten
  - **Sicherheit**: HTTPS für verschlüsselte Übertragung im lokalen Netzwerk
  - **Authentifizierung**: Optional Basic Auth oder Token-basierte Authentifizierung
- **Lehrer → KI-API**: HTTPS-Requests von Lehrer-PC zu OpenAI ChatGPT API (GPT-4o / GPT-4o-mini)

---

## Code Review Checklist

- [ ] MVVM Pattern eingehalten
- [ ] Services nur im ViewModel verwendet
- [ ] Async/await Pattern verwendet
- [ ] Proper error handling implementiert
- [ ] Code dokumentiert

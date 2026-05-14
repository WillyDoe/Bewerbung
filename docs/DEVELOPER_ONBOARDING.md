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

**Hinweis:** Für Bluetooth und Neosmartpen-Funktionen muss das Neosmartpen Windows SDK (Ordner `Windows-SDK2.0-master`) im Repository vorhanden sein; `TEI_APP.sln` bindet `Neosmartpen.Net` per **Projektreferenz** ein (wird mit der Lösung gebaut, kein manueller `bin\Debug`-Build des SDK nötig).

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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;
using tei_penService_ui.Models;

namespace tei_penService_ui.Services
{
    /// <summary>
    /// Lädt und speichert Icon-Positionen der Desktop-Arbeitsfläche (ein JSON unter dem Workspace-Stamm).
    /// </summary>
    public sealed class WorkspaceDesktopLayoutService
    {
        private static JsonSerializerSettings CreateJsonSettings()
        {
            return new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Include
            };
        }

        private readonly string _layoutFilePath;
        private readonly string _workspaceRootFull;

        public WorkspaceDesktopLayoutService(WorkspaceStorageService storage)
        {
            if (storage == null)
                throw new ArgumentNullException(nameof(storage));
            _workspaceRootFull = Path.GetFullPath(storage.WorkspaceRootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            _layoutFilePath = Path.Combine(storage.WorkspaceRootPath, WorkspaceConstants.DesktopLayoutFileName);
        }

        /// <summary>Wandelt einen absoluten Pfad in einen stabilen Layout-Schlüssel um (relativ zum Workspace).</summary>
        public string ToLayoutKey(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
                return string.Empty;
            string full = Path.GetFullPath(absolutePath);
            string root = _workspaceRootFull;
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return full.Replace(Path.AltDirectorySeparatorChar, '\\');
            string rel = full.Length > root.Length
                ? full.Substring(root.Length).Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : string.Empty;
            return string.IsNullOrEmpty(rel) ? string.Empty : rel.Replace(Path.AltDirectorySeparatorChar, '\\');
        }

        /// <summary>Liest die gespeicherten Positionen (fehlende Datei → leeres Modell).</summary>
        public WorkspaceDesktopLayoutFile Load()
        {
            try
            {
                if (!File.Exists(_layoutFilePath))
                    return new WorkspaceDesktopLayoutFile();
                string json = File.ReadAllText(_layoutFilePath);
                var data = JsonConvert.DeserializeObject<WorkspaceDesktopLayoutFile>(json, CreateJsonSettings());
                if (data?.Positions == null)
                    return new WorkspaceDesktopLayoutFile();
                return data;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WorkspaceDesktopLayoutService.Load: {ex.Message}");
                return new WorkspaceDesktopLayoutFile();
            }
        }

        /// <summary>Schreibt das komplette Layout (Aufrufer führt Merge durch).</summary>
        public void Save(WorkspaceDesktopLayoutFile file)
        {
            if (file == null)
                throw new ArgumentNullException(nameof(file));
            try
            {
                if (file.Positions == null)
                    file.Positions = new Dictionary<string, WorkspaceDesktopPointDto>(StringComparer.OrdinalIgnoreCase);
                file.SchemaVersion = WorkspaceConstants.DesktopLayoutSchemaVersion;
                string json = JsonConvert.SerializeObject(file, CreateJsonSettings());
                File.WriteAllText(_layoutFilePath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WorkspaceDesktopLayoutService.Save: {ex.Message}");
            }
        }
    }
}

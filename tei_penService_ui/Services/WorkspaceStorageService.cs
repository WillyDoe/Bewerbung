using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using tei_penService_ui.Models;

namespace tei_penService_ui.Services
{
    /// <summary>
    /// Dateisystem-Zugriff für den Workspace: Standardordner, Baum, Notiz-JSON lesen/schreiben.
    /// </summary>
    public sealed class WorkspaceStorageService
    {
        private static JsonSerializerSettings CreateJsonSettings()
        {
            return new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Include
            };
        }

        /// <summary>Serialisiert eine Notiz mit den Workspace-JSON-Einstellungen (DRY mit async-Pfad).</summary>
        public string SerializeDocument(WorkspaceInkDocumentPayload payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));
            return JsonConvert.SerializeObject(payload, CreateJsonSettings());
        }

        /// <summary>Schreibt synchron (z. B. Tab-Schluss ohne async-Wartezeit).</summary>
        public void WriteDocumentSync(string filePath, WorkspaceInkDocumentPayload payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(filePath, SerializeDocument(payload));
        }

        /// <summary>Absoluter Pfad zum Workspace-Stammverzeichnis.</summary>
        public string WorkspaceRootPath { get; }

        public WorkspaceStorageService()
        {
            WorkspaceRootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, WorkspaceConstants.WorkspaceDirectoryName);
        }

        /// <summary>Legt Stamm- und Standardordner an, falls nicht vorhanden.</summary>
        public void EnsureWorkspaceReady()
        {
            try
            {
                if (!Directory.Exists(WorkspaceRootPath))
                    Directory.CreateDirectory(WorkspaceRootPath);
                foreach (string name in WorkspaceConstants.DefaultFolderNames)
                {
                    string p = Path.Combine(WorkspaceRootPath, name);
                    if (!Directory.Exists(p))
                        Directory.CreateDirectory(p);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WorkspaceStorageService.EnsureWorkspaceReady: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Listet eine Ebene: alle Unterordner und Notizdateien im angegebenen Verzeichnis (für die Desktop-Ansicht).
        /// </summary>
        public List<WorkspaceDesktopItemInfo> ListDesktopItemsInDirectory(string directoryPath)
        {
            var list = new List<WorkspaceDesktopItemInfo>();
            try
            {
                if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
                    return list;
                string dirFull = Path.GetFullPath(directoryPath);
                if (!dirFull.StartsWith(Path.GetFullPath(WorkspaceRootPath), StringComparison.OrdinalIgnoreCase))
                    return list;
                foreach (string sub in Directory.GetDirectories(dirFull).OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
                {
                    list.Add(new WorkspaceDesktopItemInfo
                    {
                        FullPath = sub,
                        DisplayName = Path.GetFileName(sub.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? sub,
                        IsFolder = true
                    });
                }
                foreach (string file in Directory.GetFiles(dirFull).OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
                {
                    if (!file.EndsWith(WorkspaceConstants.NoteFileExtension, StringComparison.OrdinalIgnoreCase))
                        continue;
                    list.Add(new WorkspaceDesktopItemInfo
                    {
                        FullPath = file,
                        DisplayName = Path.GetFileName(file),
                        IsFolder = false
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WorkspaceStorageService.ListDesktopItemsInDirectory: {ex.Message}");
            }
            return list;
        }

        /// <summary>Baut eine hierarchische Baumstruktur unter <see cref="WorkspaceRootPath"/>.</summary>
        public ObservableCollection<WorkspaceTreeNode> BuildTreeNodes()
        {
            var roots = new ObservableCollection<WorkspaceTreeNode>();
            try
            {
                if (!Directory.Exists(WorkspaceRootPath))
                    return roots;
                foreach (string dir in Directory.GetDirectories(WorkspaceRootPath).OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
                {
                    var node = BuildFolderNode(dir);
                    if (node != null)
                        roots.Add(node);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WorkspaceStorageService.BuildTreeNodes: {ex.Message}");
            }
            return roots;
        }

        private static WorkspaceTreeNode BuildFolderNode(string directoryPath)
        {
            try
            {
                var node = new WorkspaceTreeNode
                {
                    FullPath = directoryPath,
                    DisplayName = Path.GetFileName(directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? directoryPath,
                    IsFolder = true
                };
                foreach (string sub in Directory.GetDirectories(directoryPath).OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
                {
                    var child = BuildFolderNode(sub);
                    if (child != null)
                        node.Children.Add(child);
                }
                foreach (string file in Directory.GetFiles(directoryPath).OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
                {
                    if (!file.EndsWith(WorkspaceConstants.NoteFileExtension, StringComparison.OrdinalIgnoreCase))
                        continue;
                    node.Children.Add(new WorkspaceTreeNode
                    {
                        FullPath = file,
                        DisplayName = Path.GetFileName(file),
                        IsFolder = false
                    });
                }
                return node;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WorkspaceStorageService.BuildFolderNode: {ex.Message}");
                return null;
            }
        }

        /// <summary>Erzeugt einen neuen Unterordner; <paramref name="parentDirectory"/> muss existieren.</summary>
        public string CreateChildFolder(string parentDirectory, string folderName)
        {
            if (string.IsNullOrWhiteSpace(folderName))
                throw new ArgumentException("Ordnername fehlt.", nameof(folderName));
            string trimmed = folderName.Trim();
            string combined = Path.Combine(parentDirectory ?? WorkspaceRootPath, trimmed);
            string full = Path.GetFullPath(combined);
            if (!full.StartsWith(Path.GetFullPath(WorkspaceRootPath), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Pfad außerhalb des Workspace.");
            if (Directory.Exists(full))
                throw new IOException("Ordner existiert bereits.");
            Directory.CreateDirectory(full);
            return full;
        }

        /// <summary>Löscht einen Ordner rekursiv.</summary>
        public void DeleteFolderRecursive(string directoryPath)
        {
            string full = Path.GetFullPath(directoryPath);
            if (!full.StartsWith(Path.GetFullPath(WorkspaceRootPath), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Pfad außerhalb des Workspace.");
            if (!Directory.Exists(full))
                return;
            Directory.Delete(full, true);
        }

        /// <summary>Erzeugt eine neue leere Notizdatei im angegebenen Ordner (synchron, z. B. für PEN_DOWN).</summary>
        public string CreateNewNoteFile(string parentDirectory)
        {
            if (string.IsNullOrEmpty(parentDirectory) || !Directory.Exists(parentDirectory))
                throw new DirectoryNotFoundException(parentDirectory);
            string parentFull = Path.GetFullPath(parentDirectory);
            if (!parentFull.StartsWith(Path.GetFullPath(WorkspaceRootPath), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Pfad außerhalb des Workspace.");
            string name = $"Notiz_{DateTime.Now:yyyyMMdd_HHmmss}{WorkspaceConstants.NoteFileExtension}";
            string path = Path.Combine(parentFull, name);
            var payload = new WorkspaceInkDocumentPayload();
            WriteDocumentSync(path, payload);
            return path;
        }

        /// <summary>Erzeugt eine neue leere Notizdatei im angegebenen Ordner.</summary>
        public async Task<string> CreateNewNoteFileAsync(string parentDirectory)
        {
            return await Task.Run(() => CreateNewNoteFile(parentDirectory)).ConfigureAwait(false);
        }

        /// <summary>Liest eine Notiz-JSON-Datei synchron (z. B. auf dem UI-Thread vor PEN_UP).</summary>
        public WorkspaceInkDocumentPayload ReadDocumentSync(string filePath)
        {
            string json = File.ReadAllText(filePath);
            var payload = JsonConvert.DeserializeObject<WorkspaceInkDocumentPayload>(json, CreateJsonSettings());
            return payload ?? new WorkspaceInkDocumentPayload();
        }

        /// <summary>Liest eine Notiz-JSON-Datei.</summary>
        public async Task<WorkspaceInkDocumentPayload> ReadDocumentAsync(string filePath)
        {
            return await Task.Run(() => ReadDocumentSync(filePath)).ConfigureAwait(false);
        }

        /// <summary>Schreibt eine Notiz-JSON-Datei.</summary>
        public async Task WriteDocumentAsync(string filePath, WorkspaceInkDocumentPayload payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            string json = SerializeDocument(payload);
            await Task.Run(() => File.WriteAllText(filePath, json)).ConfigureAwait(false);
        }

        /// <summary>Löscht eine Notizdatei.</summary>
        public void DeleteNoteFile(string filePath)
        {
            string full = Path.GetFullPath(filePath);
            if (!full.StartsWith(Path.GetFullPath(WorkspaceRootPath), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Pfad außerhalb des Workspace.");
            if (File.Exists(full))
                File.Delete(full);
        }

        /// <summary>Prüft, ob der Pfad eine Notizdatei unter dem Workspace ist.</summary>
        public bool IsNoteFileInWorkspace(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return false;
            if (!filePath.EndsWith(WorkspaceConstants.NoteFileExtension, StringComparison.OrdinalIgnoreCase))
                return false;
            string full = Path.GetFullPath(filePath);
            return full.StartsWith(Path.GetFullPath(WorkspaceRootPath), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Liefert den ersten Standardordner als Fallback für automatische neue Notizen.</summary>
        public string GetDefaultTargetFolderPath()
        {
            string first = Path.Combine(WorkspaceRootPath, WorkspaceConstants.DefaultFolderNames[0]);
            return Directory.Exists(first) ? first : WorkspaceRootPath;
        }

        /// <summary>Alle Verzeichnisse direkt unter dem Workspace (für schnelle Auswahl).</summary>
        public IReadOnlyList<string> GetRootSubfolderPaths()
        {
            try
            {
                if (!Directory.Exists(WorkspaceRootPath))
                    return Array.Empty<string>();
                return Directory.GetDirectories(WorkspaceRootPath).OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WorkspaceStorageService.GetRootSubfolderPaths: {ex.Message}");
                return Array.Empty<string>();
            }
        }
    }
}

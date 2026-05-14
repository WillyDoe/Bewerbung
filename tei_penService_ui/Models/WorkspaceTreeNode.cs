using System.Collections.ObjectModel;

namespace tei_penService_ui.Models
{
    /// <summary>
    /// Ein Knoten im Workspace-<see cref="System.Windows.Controls.TreeView"/> (Ordner oder Notizdatei).
    /// </summary>
    public sealed class WorkspaceTreeNode
    {
        /// <summary>Vollständiger Pfad zu Ordner oder Datei.</summary>
        public string FullPath { get; set; } = string.Empty;

        /// <summary>Anzeigename (Ordner- oder Dateiname).</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>True, wenn der Knoten ein Verzeichnis ist.</summary>
        public bool IsFolder { get; set; }

        /// <summary>Unterknoten nur bei Ordnern.</summary>
        public ObservableCollection<WorkspaceTreeNode> Children { get; } = new ObservableCollection<WorkspaceTreeNode>();
    }
}

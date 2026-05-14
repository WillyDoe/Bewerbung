using System.Collections.Generic;
using Newtonsoft.Json;

namespace tei_penService_ui.Models
{
    /// <summary>
    /// Persistente X/Y-Positionen von Desktop-Icons; Schlüssel = Pfad relativ zum Workspace-Stamm (Backslash).
    /// </summary>
    public sealed class WorkspaceDesktopLayoutFile
    {
        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; } = WorkspaceConstants.DesktopLayoutSchemaVersion;

        [JsonProperty("positions")]
        public Dictionary<string, WorkspaceDesktopPointDto> Positions { get; set; } =
            new Dictionary<string, WorkspaceDesktopPointDto>(System.StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Hilfs-DTO für JSON (Punkt auf dem Canvas).</summary>
    public sealed class WorkspaceDesktopPointDto
    {
        [JsonProperty("x")]
        public double X { get; set; }

        [JsonProperty("y")]
        public double Y { get; set; }
    }
}

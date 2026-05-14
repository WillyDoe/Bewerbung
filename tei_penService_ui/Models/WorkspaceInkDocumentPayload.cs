using Newtonsoft.Json;

namespace tei_penService_ui.Models
{
    /// <summary>
    /// Serialisierbare Notiz: erkannter Text (UTF-8 als String im JSON) und Freihand als ISF (Base64).
    /// </summary>
    public sealed class WorkspaceInkDocumentPayload
    {
        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; } = WorkspaceConstants.CurrentSchemaVersion;

        /// <summary>Gefilterter Erkennungstext (Zeichenkette; Datei-Encoding UTF-8).</summary>
        [JsonProperty("recognizedTextUtf8")]
        public string RecognizedTextUtf8 { get; set; } = string.Empty;

        /// <summary>WPF-Ink im ISF-Format, Base64-kodiert.</summary>
        [JsonProperty("inkIsfBase64")]
        public string InkIsfBase64 { get; set; } = string.Empty;
    }
}

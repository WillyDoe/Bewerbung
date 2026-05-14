using TeiPenServiceConnectionManager.Models;

namespace tei_penService_ui.Models
{
    /// <summary>
    /// Anzeige-Daten für einen verbundenen Stift (aus GetConnectedPenInfo).
    /// Wird in der Stiftliste unter "Verbundenes Gerät" verwendet, da die
    /// Discovered-Liste beim Stopp der Gerätesuche geleert wird.
    /// </summary>
    public class ConnectedPenDisplayInfo
    {
        public string Name { get; }
        public string MacAddress { get; }
        public string Id { get; }

        public ConnectedPenDisplayInfo(PenConnectionInfoModel connectionInfo)
        {
            if (connectionInfo == null)
            {
                Name = string.Empty;
                MacAddress = string.Empty;

                Id = string.Empty;
                return;
            }
            Name = connectionInfo.PenName ?? connectionInfo.DisplayName ?? string.Empty;
            MacAddress = connectionInfo.MacAddress ?? string.Empty;
            Id = connectionInfo.DeviceId ?? string.Empty;
        }
    }
}

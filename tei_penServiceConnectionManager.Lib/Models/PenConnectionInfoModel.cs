using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TeiPenServiceConnectionManager.Models
{
    /// <summary>
    /// Modell für die Verbindungsinformationen eines Smartpens.
    /// Hinweis: Diese Klasse ist nicht thread-safe. Alle Schreibzugriffe müssen durch den aufrufenden Code synchronisiert werden.
    /// </summary>
    public class PenConnectionInfoModel : INotifyPropertyChanged
    {
        // Windows.DeviceInformation + PropertyBag
        private string _deviceId = string.Empty;
        public string DeviceId
        {
            get => _deviceId;
            set => SetField(ref _deviceId, value);
        }

        private string _displayName = string.Empty;
        public string DisplayName
        {
            get => _displayName;
            set => SetField(ref _displayName, value);
        }

        private string _macAddress = string.Empty;
        public string MacAddress
        {
            get => _macAddress;
            set => SetField(ref _macAddress, value);
        }

        private PenConnectionState _connectionState = PenConnectionState.Disconnected;
        public PenConnectionState ConnectionState
        {
            get => _connectionState;
            set => SetField(ref _connectionState, value);
        }

        /// <summary>
        /// Computed Property für Rückwärtskompatibilität.
        /// Gibt true zurück, wenn ConnectionState == Authenticated ist.
        /// </summary>
        public bool IsConnected => ConnectionState == PenConnectionState.Authenticated;

        private int? _rssi;
        public int? Rssi
        {
            get => _rssi;
            set => SetField(ref _rssi, value);
        }

        // SDK: GenericBluetoothPenClient
        private bool _connectedDeviceIsLe;
        public bool ConnectedDeviceIsLe
        {
            get => _connectedDeviceIsLe;
            set => SetField(ref _connectedDeviceIsLe, value);
        }

        private bool _clientAlive;
        public bool ClientAlive
        {
            get => _clientAlive;
            set => SetField(ref _clientAlive, value);
        }

        // SDK: PenInformation
        private string _penName = string.Empty;
        public string PenName
        {
            get => _penName;
            set => SetField(ref _penName, value);
        }

        private bool _penIsLe;
        public bool PenIsLe
        {
            get => _penIsLe;
            set => SetField(ref _penIsLe, value);
        }

        private int _protocol;
        public int Protocol
        {
            get => _protocol;
            set => SetField(ref _protocol, value);
        }

        private ulong? _virtualMacAddress;
        public ulong? VirtualMacAddress
        {
            get => _virtualMacAddress;
            set => SetField(ref _virtualMacAddress, value);
        }

        // SDK: PenUpdateInformation
        private string _modelName = string.Empty;
        public string ModelName
        {
            get => _modelName;
            set => SetField(ref _modelName, value);
        }

        private int? _updateRssi;
        public int? UpdateRssi
        {
            get => _updateRssi;
            set => SetField(ref _updateRssi, value);
        }

        // SDK: PenStatusReceivedEventArgs
        private int? _batteryStatus;
        /// <summary>
        /// Batteriestatus des Stifts in Prozent (0-100).
        /// null, wenn der Batteriestatus noch nicht abgerufen wurde.
        /// </summary>
        public int? BatteryStatus
        {
            get => _batteryStatus;
            set => SetField(ref _batteryStatus, value);
        }

        // Verbindungsinformationen
        private bool _wasAutoConnected;
        /// <summary>
        /// Gibt an, ob die Verbindung automatisch hergestellt wurde (z.B. durch AutoConnectCallback oder automatisches Passwort).
        /// </summary>
        public bool WasAutoConnected
        {
            get => _wasAutoConnected;
            set => SetField(ref _wasAutoConnected, value);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
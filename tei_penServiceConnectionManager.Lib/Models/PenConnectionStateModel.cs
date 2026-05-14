namespace TeiPenServiceConnectionManager.Models
{
    /// <summary>
    /// Repräsentiert den Verbindungsstatus eines Smartpens.
    /// </summary>
    public enum PenConnectionState
    {
        /// <summary>
        /// Keine Verbindung vorhanden.
        /// </summary>
        Disconnected,
        
        /// <summary>
        /// Physische Bluetooth-Verbindung hergestellt, aber noch nicht authentifiziert.
        /// </summary>
        Connected,
        
        /// <summary>
        /// Passwort wird benötigt für die Authentifizierung.
        /// </summary>
        PasswordRequired,
        
        /// <summary>
        /// Vollständig authentifiziert, Datenübertragung möglich.
        /// </summary>
        Authenticated
    }
}


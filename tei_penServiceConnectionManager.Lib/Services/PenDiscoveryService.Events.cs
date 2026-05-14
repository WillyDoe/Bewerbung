using System;
using Neosmartpen.Net;
using Neosmartpen.Net.Bluetooth;
using TeiPenServiceConnectionManager.Utilities;
using Windows.Devices.Bluetooth;

#nullable enable

namespace TeiPenServiceConnectionManager.Services
{
    /// <summary>
    /// Event-Handler-Teil des PenDiscoveryService (partielle Klasse).
    /// </summary>
    public partial class PenDiscoveryService
    {
        /// <summary>
        /// Event-Handler: Gerät gefunden.
        /// </summary>
        private void OnDeviceInReach(IPenClient sender, PenInformation deviceInfo)
        {
            ThreadSafeConsole.WriteLine($"Gerät {deviceInfo.Id} gefunden.");
            if (_isDisposedCallback())
            {
                return;
            }

            if (deviceInfo == null)
            {
                ThreadSafeConsole.WriteLine("Ungültiges Gerät gefunden.");
                return;
            }

            string? macAddress = deviceInfo.MacAddress;
            if (string.IsNullOrEmpty(macAddress))
            {
                macAddress = deviceInfo.Id;
                if (string.IsNullOrEmpty(macAddress))
                {
                    ThreadSafeConsole.WriteLine("Ungültiges Gerät gefunden (keine MAC-Adresse und keine ID).");
                    return;
                }
            }

            macAddress = macAddress.ToUpperInvariant();

            if (!PenConnectionStrengthChecker.IsRssiValid(deviceInfo.Rssi))
            {
                ThreadSafeConsole.WriteLine($"Gerät {macAddress} hat einen ungültigen RSSI-Wert: {deviceInfo.Rssi}");
                return;
            }

            try
            {
                _addPenToReachCallback(deviceInfo);
                ThreadSafeConsole.WriteLine($"Gerät {macAddress} ist in Reichweite.");
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler beim Hinzufügen des Geräts {macAddress}: {ex.Message}");
            }
        }

        /// <summary>
        /// Event-Handler: Gerät aktualisiert.
        /// </summary>
        private void OnDeviceInReachUpdated(IPenClient sender, PenUpdateInformation deviceUpdateInfo)
        {
            if (_isDisposedCallback())
            {
                return;
            }

            if (deviceUpdateInfo == null)
            {
                return;
            }

            var pensInReach = _getPensInReachCallback();
            string? macAddress = null;
            PenInformation? existingPen = null;

            foreach (var kvp in pensInReach)
            {
                if (kvp.Value.Id == deviceUpdateInfo.Id)
                {
                    macAddress = kvp.Key;
                    existingPen = kvp.Value;
                    break;
                }
            }

            if (existingPen == null && !string.IsNullOrEmpty(deviceUpdateInfo.Id))
            {
                foreach (var kvp in pensInReach)
                {
                    string keyMac = kvp.Key;
                    if (deviceUpdateInfo.Id.IndexOf(keyMac, StringComparison.OrdinalIgnoreCase) >= 0
                        || deviceUpdateInfo.Id.IndexOf(keyMac.Replace(":", ""), StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        macAddress = kvp.Key;
                        existingPen = kvp.Value;
                        break;
                    }
                }
            }

            if (existingPen == null || string.IsNullOrEmpty(macAddress))
            {
                return;
            }

            if (!PenConnectionStrengthChecker.IsRssiValid(deviceUpdateInfo.Rssi))
            {
                DateTime now = DateTime.UtcNow;
                _updatePenLastUpdateTimestampCallback(macAddress!, now);
                _updatePenOutOfRangeTimestampCallback(macAddress!, null);
                return;
            }

            if (_isDisposedCallback())
            {
                return;
            }

            if (pensInReach.TryGetValue(macAddress!, out PenInformation currentPen))
            {
                try
                {
                    currentPen.Update(deviceUpdateInfo);
                }
                catch (Exception ex)
                {
                    ThreadSafeConsole.WriteLine($"Fehler beim Aktualisieren des Geräts {macAddress}: {ex.Message}");
                    return;
                }

                DateTime now = DateTime.UtcNow;
                _updatePenLastUpdateTimestampCallback(macAddress!, now);

                if (_notifyPenUpdatedCallback != null)
                {
                    try
                    {
                        _notifyPenUpdatedCallback(currentPen);
                    }
                    catch (Exception ex)
                    {
                        ThreadSafeConsole.WriteLine($"Fehler beim Aufruf von notifyPenUpdatedCallback für {macAddress}: {ex.Message}");
                    }
                }

                if (_isPenConnectedCallback(macAddress!))
                {
                    _updatePenOutOfRangeTimestampCallback(macAddress!, null);
                    return;
                }

                bool isInRange = PenConnectionStrengthChecker.PenIsStillInRange(currentPen);

                if (!isInRange)
                {
                    var timestamps = _getPenOutOfRangeTimestampsCallback();
                    if (timestamps.TryGetValue(macAddress!, out DateTime? existingTimestamp))
                    {
                        if (!existingTimestamp.HasValue)
                        {
                            _updatePenOutOfRangeTimestampCallback(macAddress!, now);
                        }
                    }
                    else
                    {
                        _updatePenOutOfRangeTimestampCallback(macAddress!, now);
                    }

                    timestamps = _getPenOutOfRangeTimestampsCallback();
                    if (timestamps.TryGetValue(macAddress!, out DateTime? outOfRangeSince))
                    {
                        if (outOfRangeSince.HasValue)
                        {
                            TimeSpan timeSinceOutOfRange = now - outOfRangeSince.Value;
                            if (timeSinceOutOfRange.TotalSeconds >= _outOfRangeTimeoutSeconds)
                            {
                                _removePenFromReachCallback(macAddress!);
                                ThreadSafeConsole.WriteLine($"tei-Pen {macAddress} ist länger als {_outOfRangeTimeoutSeconds} Sekunden außerhalb der Reichweite, also aus der Liste entfernt.");
                            }
                        }
                    }
                }
                else
                {
                    _updatePenOutOfRangeTimestampCallback(macAddress!, null);
                }
            }
            else
            {
                ThreadSafeConsole.WriteLine($"Gerät {macAddress} nicht mehr gefunden.");
            }
        }

        /// <summary>
        /// Event-Handler: Suche gestoppt.
        /// </summary>
        private void OnSearchStopped(IPenClient sender, BluetoothError error)
        {
            bool wasSearching = false;
            lock (_isSearchingLock)
            {
                if (_isSearching)
                {
                    _isSearching = false;
                    wasSearching = true;
                    UnsubscribeFromSDKBluetoothEvents();
                }
            }

            if (wasSearching)
            {
                StopNoUpdateMonitoring();
            }

            if (!wasSearching)
            {
                ThreadSafeConsole.WriteLine("Gerätesuche ist nicht aktiv.");
                return;
            }

            if (error == BluetoothError.Success)
            {
                ThreadSafeConsole.WriteLine("Gerätesuche erfolgreich gestoppt.");
            }
            else
            {
                ThreadSafeConsole.WriteLine($"Gerätesuche mit Fehlercode {error} gestoppt.");
            }

            _onDeviceDiscoveryStatusChangedCallback?.Invoke(false);
        }
    }
}

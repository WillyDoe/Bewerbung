using System;
using System.Threading.Tasks;
using Neosmartpen.Net;
using Neosmartpen.Net.Bluetooth;
using TeiPenServiceConnectionManager.Models;
using TeiPenServiceConnectionManager.Utilities;

#nullable enable

namespace TeiPenServiceConnectionManager.Services
{
    /// <summary>
    /// Event-Handler-Teil des TeiPenService (partielle Klasse).
    /// </summary>
    public partial class TeiPenService
    {
        /// <summary>
        /// Feuert das BluetoothStatusChanged Event thread-safe.
        /// </summary>
        private void OnBluetoothStatusChanged(bool isEnabled)
        {
            if (IsDisposed())
            {
                return;
            }

            if (!isEnabled)
            {
                StopDeviceDiscovery();
                // SinglePenConnectionManager trennen und entfernen – Verbindung ist bei ausgeschaltetem Bluetooth ohnehin ungültig
                _ = DisconnectFromPenAsync().ContinueWith(t =>
                {
                    if (t.IsFaulted && t.Exception != null)
                        ThreadSafeConsole.WriteLine($"Fehler beim Trennen bei Bluetooth-Aus: {t.Exception.InnerException?.Message ?? t.Exception.Message}");
                }, TaskContinuationOptions.OnlyOnFaulted);
            }

            try
            {
                BluetoothStatusChanged?.Invoke(this, isEnabled);
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler beim Auslösen des BluetoothStatusChanged Event: {ex.Message}");
            }
        }

        /// <summary>
        /// Feuert das ConnectionStatusChanged Event thread-safe.
        /// </summary>
        private void OnConnectionStatusChanged(bool isConnected)
        {
            if (IsDisposed())
            {
                return;
            }

            try
            {
                ConnectionStatusChanged?.Invoke(this, isConnected);
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler beim Auslösen des ConnectionStatusChanged Event: {ex.Message}");
            }
        }

        /// <summary>
        /// Feuert das DeviceDiscoveryStatusChanged Event thread-safe.
        /// </summary>
        private void OnDeviceDiscoveryStatusChanged(bool isActive)
        {
            if (IsDisposed())
            {
                return;
            }

            try
            {
                DeviceDiscoveryStatusChanged?.Invoke(this, isActive);
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler beim Auslösen des DeviceDiscoveryStatusChanged Event: {ex.Message}");
            }
        }

        /// <summary>
        /// Feuert das PenDiscovered Event thread-safe.
        /// </summary>
        private void OnPenDiscovered(PenInformation penInformation)
        {
            if (IsDisposed())
            {
                return;
            }

            try
            {
                PenDiscovered?.Invoke(this, penInformation);
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler beim Auslösen des PenDiscovered Event: {ex.Message}");
            }
        }

        /// <summary>
        /// Feuert das PenRemoved Event thread-safe.
        /// </summary>
        private void OnPenRemoved(PenInformation penInformation)
        {
            if (IsDisposed())
            {
                return;
            }

            try
            {
                PenRemoved?.Invoke(this, penInformation);
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler beim Auslösen des PenRemoved Event: {ex.Message}");
            }
        }

        /// <summary>
        /// Feuert das PenUpdated Event thread-safe.
        /// </summary>
        private void OnPenUpdated(PenInformation penInformation)
        {
            if (IsDisposed())
            {
                return;
            }

            try
            {
                PenUpdated?.Invoke(this, penInformation);
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler beim Auslösen des PenUpdated Event: {ex.Message}");
            }
        }
    }
}

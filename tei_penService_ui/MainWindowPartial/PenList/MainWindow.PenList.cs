using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Neosmartpen.Net.Bluetooth;
using TeiPenServiceConnectionManager.Models;
using tei_penService_ui.Controls;
using tei_penService_ui.Helpers;
using tei_penService_ui.Models;
using tei_penService_ui.Services;

namespace tei_penService_ui
{
    /// <summary>
    /// Stiftlisten (Verfügbare/Gekoppelte Geräte), Connect/Disconnect/Remove und eventbasierte Aktualisierung für MainWindow.
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>True, wenn die letzte Verbindungsanfrage von einem PairedPenControl (Tab Gekoppelte Geräte) kam.</summary>
        private volatile bool _connectRequestedFromPairedControl;

        /// <summary>Lock für Zugriff auf <see cref="_connectingToMac"/>; verhindert doppelte Connect-Aufrufe.</summary>
        private readonly object _connectLock = new object();
        /// <summary>MAC-Adresse, zu der gerade verbunden wird; null wenn kein Connect läuft.</summary>
        private string _connectingToMac;

        /// <summary>
        /// Event-Handler, wenn ein neuer Stift entdeckt wurde.
        /// </summary>
        private void OnPenDiscovered(object sender, PenInformation penInformation)
        {
            _ = UpdateDiscoveredPensAsync();
        }

        /// <summary>
        /// Event-Handler, wenn ein Stift außer Reichweite gegangen ist.
        /// </summary>
        private void OnPenRemoved(object sender, PenInformation penInformation)
        {
            RemovePenControlFromList(penInformation);
        }

        /// <summary>
        /// Event-Handler, wenn sich die Daten eines Stifts in Reichweite geändert haben (z. B. RSSI).
        /// </summary>
        private void OnPenUpdated(object sender, PenInformation penInformation)
        {
            _ = UpdateDiscoveredPensAsync();
        }

        /// <summary>
        /// Aktualisiert die Liste der gefundenen Stifte und die gekoppelte Liste (mit Live-RSSI aus Discovered).
        /// </summary>
        private async Task UpdateDiscoveredPensAsync()
        {
            if (_teiPenServiceWrapper == null)
                return;

            try
            {
                bool isSearchActive = await _teiPenServiceWrapper.IsDeviceSearchActiveAsync();
                if (!isSearchActive)
                {
                    bool hasConnectedPen = false;
                    try { hasConnectedPen = _teiPenServiceWrapper?.GetConnectedPenInfo() != null; } catch { }
                    if (!hasConnectedPen)
                        ClearDiscoveredPens();
                    await UpdatePairedPensAsync().ConfigureAwait(false);
                    return;
                }

                var discoveredPens = await _teiPenServiceWrapper.GetDiscoveredPensAsync();
                await Dispatcher.InvokeAsync(() => UpdatePenListUI(discoveredPens));
                await UpdatePairedPensAsync(discoveredPens).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler beim Aktualisieren der Stiftliste: {ex.Message}");
            }
        }

        /// <summary>
        /// Abonniert Passwort-OK für ein Stift-Control (wird bei Listen-Updates idempotent neu gesetzt).
        /// </summary>
        private void AttachPenPasswordHandlers(PenControlBase penControl)
        {
            if (penControl == null)
                return;
            penControl.PasswordSubmitted -= OnPenPasswordSubmitted;
            penControl.PasswordSubmitted += OnPenPasswordSubmitted;
        }

        private void AttachConnectionHandlers(PenControlBase penControl)
        {
            if (penControl == null)
                return;

            penControl.ConnectRequested -= OnPenConnectRequested;
            penControl.ConnectRequested += OnPenConnectRequested;
            penControl.DisconnectRequested -= OnPenDisconnectRequested;
            penControl.DisconnectRequested += OnPenDisconnectRequested;
        }

        /// <summary>
        /// Sucht ein PenControl zur MAC (Tab Verfügbare oder Gekoppelte Geräte).
        /// </summary>
        private PenControl FindPenControlByMac(string normalizedMac)
        {
            if (string.IsNullOrEmpty(normalizedMac))
                return null;
            var sv = GetStartView();
            if (sv?.PenListContainer != null)
            {
                foreach (var item in sv.PenListContainer.Items)
                {
                    if (item is PenControl pc)
                    {
                        string m = MacAddressHelper.NormalizeMacAddress(pc.PenInformation);
                        if (!string.IsNullOrEmpty(m) && string.Equals(m, normalizedMac, StringComparison.OrdinalIgnoreCase))
                            return pc;
                    }
                }
            }
            if (sv?.PairedPenListContainer != null)
            {
                foreach (var item in sv.PairedPenListContainer.Items)
                {
                    if (item is PenControl pc)
                    {
                        string m = MacAddressHelper.NormalizeMacAddress(pc.PenInformation);
                        if (!string.IsNullOrEmpty(m) && string.Equals(m, normalizedMac, StringComparison.OrdinalIgnoreCase))
                            return pc;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Zeigt die Passwortzeile, wenn die Lib meldet, dass der Stift ein Passwort verlangt.
        /// </summary>
        private void OnWrapperPasswordRequired(object sender, string macAddress)
        {
            try
            {
                string normalized = NormalizeMacFromPasswordCallback(macAddress);
                if (string.IsNullOrEmpty(normalized))
                    return;
                _ = Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        FindPenControlByMac(normalized)?.ShowPasswordInputRequired();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"OnWrapperPasswordRequired UI: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnWrapperPasswordRequired: {ex.Message}");
            }
        }

        private static string NormalizeMacFromPasswordCallback(string macAddress)
        {
            if (string.IsNullOrWhiteSpace(macAddress))
                return null;
            return macAddress.Trim().Replace("-", ":").ToUpperInvariant();
        }

        /// <summary>
        /// Sendet das Passwort an den TeiPenServiceWrapper; bei Erfolg wird die Maske geschlossen.
        /// </summary>
        private void OnPenPasswordSubmitted(object sender, string password)
        {
            try
            {
                if (_teiPenServiceWrapper == null)
                    return;
                if (string.IsNullOrEmpty(password))
                {
                    if (sender is PenControl pcEmpty)
                        pcEmpty.SetPasswordInputError(true);
                    return;
                }
                bool ok = _teiPenServiceWrapper.InputPassword(password);
                if (sender is PenControl penControl)
                {
                    if (ok)
                        penControl.HidePasswordInputAfterSubmit();
                    else
                        penControl.SetPasswordInputError(true);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnPenPasswordSubmitted: {ex.Message}");
                if (sender is PenControl pc)
                    pc.SetPasswordInputError(true);
            }
        }

        /// <summary>
        /// Aktualisiert die UI der Stiftliste. Bestehende StandardPenControls werden wiederverwendet.
        /// </summary>
        private void UpdatePenListUI(IReadOnlyCollection<PenInformation> discoveredPens)
        {
            var sv = GetStartView();
            if (sv?.PenListContainer == null)
                return;
            var penListContainer = sv.PenListContainer;
            var contentAvailable = sv.ContentAvailable;

            string connectedMac = null;
            try
            {
                var connectedInfo = _teiPenServiceWrapper?.GetConnectedPenInfo();
                if (connectedInfo != null)
                    connectedMac = MacAddressHelper.NormalizeMacAddress(new ConnectedPenDisplayInfo(connectedInfo));
            }
            catch { }

            var existingByMac = new Dictionary<string, StandardPenControl>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in penListContainer.Items.Cast<object>().ToList())
            {
                if (item is StandardPenControl penControl && penControl.PenInformation != null)
                {
                    string mac = MacAddressHelper.NormalizeMacAddress(penControl.PenInformation);
                    if (!string.IsNullOrEmpty(mac))
                        existingByMac[mac] = penControl;
                }
            }

            StandardPenControl connectedPenControlToPreserve = null;
            if (!string.IsNullOrEmpty(connectedMac))
            {
                if (!existingByMac.TryGetValue(connectedMac, out connectedPenControlToPreserve))
                {
                    foreach (var item in penListContainer.Items.Cast<object>().ToList())
                    {
                        if (item is StandardPenControl sc && sc.PenInformation != null
                            && string.Equals(MacAddressHelper.NormalizeMacAddress(sc.PenInformation), connectedMac, StringComparison.OrdinalIgnoreCase))
                        {
                            connectedPenControlToPreserve = sc;
                            break;
                        }
                    }
                }
            }

            penListContainer.Items.Clear();

            var sortedPens = discoveredPens.OrderByDescending(pen => pen.Rssi).ToList();
            int index = 0;
            foreach (var pen in sortedPens)
            {
                string mac = MacAddressHelper.NormalizeMacAddress(pen);
                if (string.IsNullOrEmpty(mac))
                    continue;

                StandardPenControl penControl;
                if (existingByMac.TryGetValue(mac, out var existing))
                {
                    penControl = existing;
                    penControl.PenInformation = pen;
                    penControl.Index = index;
                }
                else
                {
                    penControl = new StandardPenControl
                    {
                        PenInformation = pen,
                        Index = index
                    };
                    penControl.ConnectionStateChanged += OnPenConnectionStateChanged;
                }

                AttachConnectionHandlers(penControl);
                AttachPenPasswordHandlers(penControl);

                penListContainer.Items.Add(penControl);
                index++;
            }

            if (connectedPenControlToPreserve != null && !sortedPens.Any(pen => string.Equals(MacAddressHelper.NormalizeMacAddress(pen), connectedMac, StringComparison.OrdinalIgnoreCase)))
            {
                // Verbundenes Control wird bewusst beibehalten, damit der Nutzer den aktiven Stift weiter sieht,
                // auch wenn Discovery kurzzeitig keine Ergebnisse liefert.
                AttachConnectionHandlers(connectedPenControlToPreserve);
                AttachPenPasswordHandlers(connectedPenControlToPreserve);
                connectedPenControlToPreserve.Index = penListContainer.Items.Count;
                penListContainer.Items.Add(connectedPenControlToPreserve);
            }

            if (contentAvailable != null)
                contentAvailable.IsHitTestVisible = penListContainer.Items.Count > 0;

            UpdateStartButtonText();
        }

        /// <summary>
        /// Lädt gekoppelte Stifte aus dem Memory und füllt die Liste "Gekoppelte Geräte".
        /// </summary>
        private async Task UpdatePairedPensAsync(IReadOnlyCollection<PenInformation> discoveredPens = null)
        {
            var sv = GetStartView();
            if (sv?.PairedPenListContainer == null)
                return;
            if (_teiPenServiceWrapper == null)
                return;

            try
            {
                var paired = await _teiPenServiceWrapper.GetPairedPensAsync().ConfigureAwait(false);
                IReadOnlyDictionary<string, PenMemoryEntry> appDataPens = null;
                if (_appDataService != null)
                {
                    try
                    {
                        appDataPens = await _appDataService.GetPairedPensAsync().ConfigureAwait(false);
                    }
                    catch { }
                }
                if (discoveredPens == null && await _teiPenServiceWrapper.IsDeviceSearchActiveAsync().ConfigureAwait(false))
                    discoveredPens = await _teiPenServiceWrapper.GetDiscoveredPensAsync().ConfigureAwait(false);
                discoveredPens = discoveredPens ?? Array.Empty<PenInformation>();

                PenConnectionInfoModel connectedInfo = null;
                try
                {
                    connectedInfo = _teiPenServiceWrapper?.GetConnectedPenInfo();
                }
                catch { }
                string connectedMac = connectedInfo != null ? AppDataService.NormalizeMacAddressCanonical(new ConnectedPenDisplayInfo(connectedInfo).MacAddress ?? "") : null;
                if (string.IsNullOrEmpty(connectedMac))
                    connectedMac = null;

                var discoveredByMac = new Dictionary<string, PenInformation>(StringComparer.OrdinalIgnoreCase);
                foreach (var pen in discoveredPens)
                {
                    if (pen == null || string.IsNullOrEmpty(pen.MacAddress)) continue;
                    string mac = AppDataService.NormalizeMacAddressCanonical(pen.MacAddress);
                    if (!string.IsNullOrEmpty(mac))
                        discoveredByMac[mac] = pen;
                }

                var pairedList = paired;
                var discoveredLookup = discoveredByMac;
                IReadOnlyDictionary<string, PenMemoryEntry> appDataLookup = null;
                if (appDataPens != null && appDataPens.Count > 0)
                {
                    var byCanonical = new Dictionary<string, PenMemoryEntry>(StringComparer.OrdinalIgnoreCase);
                    foreach (var kv in appDataPens)
                    {
                        string c = AppDataService.NormalizeMacAddressCanonical(kv.Key);
                        if (!string.IsNullOrEmpty(c))
                            byCanonical[c] = kv.Value;
                    }
                    appDataLookup = byCanonical;
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    var existingByMac = new Dictionary<string, PairedPenControl>(StringComparer.OrdinalIgnoreCase);
                    foreach (var item in sv.PairedPenListContainer.Items.Cast<object>().ToList())
                    {
                        if (item is PairedPenControl pc && pc.PenInformation is PairedPenDisplayInfo p)
                        {
                            string mac = AppDataService.NormalizeMacAddressCanonical(p.MacAddress ?? "");
                            if (!string.IsNullOrEmpty(mac))
                                existingByMac[mac] = pc;
                        }
                    }

                    sv.PairedPenListContainer.Items.Clear();
                    int index = 0;
                    foreach (var kv in pairedList)
                    {
                        var entry = kv.Value;
                        if (entry == null || string.IsNullOrEmpty(entry.MacAddress))
                            continue;
                        string mac = AppDataService.NormalizeMacAddressCanonical(entry.MacAddress ?? "");
                        if (string.IsNullOrEmpty(mac))
                            continue;
                        if (appDataLookup != null && appDataLookup.TryGetValue(mac, out var appEntry))
                        {
                            entry = new PenMemoryEntry
                            {
                                MacAddress = entry.MacAddress,
                                DeviceId = entry.DeviceId,
                                PenName = !string.IsNullOrEmpty(appEntry.PenName) ? appEntry.PenName : entry.PenName,
                                DisplayName = entry.DisplayName,
                                Protocol = entry.Protocol,
                                FirstConnectedAt = entry.FirstConnectedAt,
                                LastConnectedAt = entry.LastConnectedAt,
                                Password = entry.Password
                            };
                        }
                        PenInformation discoveredPen = null;
                        if (!string.IsNullOrEmpty(mac))
                            discoveredLookup.TryGetValue(mac, out discoveredPen);
                        bool inRange = discoveredPen != null;
                        int liveRssi = inRange ? discoveredPen.Rssi : 0;
                        var displayInfo = new PairedPenDisplayInfo(entry, inRange, liveRssi);

                        PairedPenControl penControl;
                        if (existingByMac.TryGetValue(mac, out var existing))
                        {
                            penControl = existing;
                            penControl.PenInformation = displayInfo;
                            penControl.Index = index;
                        }
                        else
                        {
                            penControl = new PairedPenControl
                            {
                                PenInformation = displayInfo,
                                Index = index
                            };
                            penControl.ConnectionStateChanged += OnPenConnectionStateChanged;
                            AttachConnectionHandlers(penControl);
                            penControl.RemoveRequested += OnPenRemoveRequested;
                        }

                        AttachConnectionHandlers(penControl);
                        AttachPenPasswordHandlers(penControl);

                        sv.PairedPenListContainer.Items.Add(penControl);

                        if (!string.IsNullOrEmpty(connectedMac) && string.Equals(mac, connectedMac, StringComparison.Ordinal))
                            penControl.SetConnectionSucceeded();
                        else
                            penControl.SetDisconnected();

                        index++;
                    }

                  _ = Dispatcher.InvokeAsync(() =>
                    {
                        foreach (var item in sv.PairedPenListContainer.Items)
                        {
                            if (item is PairedPenControl pc)
                                pc.RefreshHoverState();
                        }
                    }, DispatcherPriority.Loaded);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler beim Laden der gekoppelten Stifte: {ex.Message}");
            }
        }

        /// <summary>
        /// Aktualisiert den Text des Start-Buttons: "Starten" wenn ein Stift verbunden ist, sonst "ohne Stift starten".
        /// </summary>
        private void UpdateStartButtonText()
        {
            var sv = GetStartView();
            if (sv?.StartWithoutPenButton == null)
                return;
            bool anyConnected = false;
            if (sv.PenListContainer != null)
            {
                foreach (var item in sv.PenListContainer.Items)
                {
                    if (item is PenControlBase pc && pc.IsConnected)
                    {
                        anyConnected = true;
                        break;
                    }
                }
            }
            sv.StartWithoutPenButton.ButtonText = anyConnected ? "Starten" : "ohne Stift starten";
        }

        /// <summary>Event-Handler, wenn sich der Verbindungsstatus eines PenControls geändert hat. Aktualisiert Start-Button, Hover-Brush und Status-Indikator.</summary>
        private void OnPenConnectionStateChanged(object sender, bool isConnected)
        {
            UpdateStartButtonText();
            UpdatePairDeviceButtonHoverBrush();
            UpdatePenConnectedStatusIndicator();
        }

        /// <summary>Behandelt Verbindungsanfrage vom PenControl. Verbindet mit dem Stift (MAC), verhindert Doppelaufrufe.</summary>
        private async void OnPenConnectRequested(object sender, object penInfo)
        {
            _connectRequestedFromPairedControl = sender is PairedPenControl;
            if (penInfo == null || _teiPenServiceWrapper == null)
            {
                await Dispatcher.InvokeAsync(() => (sender as PenControlBase)?.SetConnectionFailed());
                _connectRequestedFromPairedControl = false;
                return;
            }

            string mac = MacAddressHelper.NormalizeMacAddress(penInfo);
            if (string.IsNullOrEmpty(mac))
            {
                await Dispatcher.InvokeAsync(() => (sender as PenControlBase)?.SetConnectionFailed());
                _connectRequestedFromPairedControl = false;
                return;
            }

            lock (_connectLock)
            {
                if (string.Equals(_connectingToMac, mac, StringComparison.Ordinal))
                    return;
                _connectingToMac = mac;
            }

            bool success = false;
            try
            {
                success = await _teiPenServiceWrapper.ConnectToPenAsync(mac).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler beim Verbinden mit Stift {mac}: {ex.Message}");
            }
            finally
            {
                lock (_connectLock) _connectingToMac = null;
            }

            await Dispatcher.InvokeAsync(() =>
            {
                if (success)
                {
                    if (sender is PairedPenControl ppc)
                    {
                        ppc.SetConnectionSucceeded();
                        ShowConnectedPenInListWhenReady();
                    }
                    else
                    {
                        var connectedInfo = _teiPenServiceWrapper?.GetConnectedPenInfo();
                        if (connectedInfo != null)
                            ShowConnectedPenInList(connectedInfo);
                        else
                            (sender as PenControlBase)?.SetConnectionFailed();
                    }
                }
                else
                {
                    (sender as PenControlBase)?.SetConnectionFailed();
                    _connectRequestedFromPairedControl = false;
                }
            });
        }

        /// <summary>Event-Handler für Verbindungsstatus-Änderung des Wrappers. Stoppt ggf. Gerätesuche, aktualisiert UI und Listen.</summary>
        private async void OnWrapperConnectionStatusChanged(object sender, bool isConnected)
        {
            UpdateStartButtonText();

            if (isConnected && _teiPenServiceWrapper != null)
            {
                try
                {
                    bool isSearchActive = await _teiPenServiceWrapper.IsDeviceSearchActiveAsync();
                    if (isSearchActive)
                        await _teiPenServiceWrapper.StopDeviceSearchAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Fehler beim Beenden der Gerätesuche nach Verbindung: {ex.Message}");
                }
            }

            UpdateDeviceSectionVisibility(true);
            UpdatePairDeviceButtonHoverBrush();
            UpdatePenConnectedStatusIndicator();
            if (isConnected)
            {
                try
                {
                    var connectedInfo = _teiPenServiceWrapper?.GetConnectedPenInfo();
                    if (connectedInfo != null && !_connectRequestedFromPairedControl)
                        ShowConnectedPenInList(connectedInfo);
                    if (_connectRequestedFromPairedControl)
                        _connectRequestedFromPairedControl = false;
                    _ = UpdatePairedPensAsync();
                    _ = _appDataService?.SyncPairedPensFromLibAsync();
                }
                catch { }
            }
            else
            {
                ClearDiscoveredPens();
                _ = UpdatePairedPensAsync();
                _ = _appDataService?.SyncPairedPensFromLibAsync();
            }
        }

        /// <summary>
        /// Behandelt Entfernungsanfrage für einen gekoppelten Stift.
        /// </summary>
        private async void OnPenRemoveRequested(object sender, object penInfo)
        {
            if (penInfo == null || _teiPenServiceWrapper == null)
                return;

            string mac = MacAddressHelper.NormalizeMacAddress(penInfo);
            if (string.IsNullOrEmpty(mac))
                return;

            try
            {
                bool removed = await _teiPenServiceWrapper.RemovePairedPenAsync(mac).ConfigureAwait(false);
                if (removed)
                {
                    var connectedInfo = _teiPenServiceWrapper.GetConnectedPenInfo();
                    string connectedMac = connectedInfo != null ? MacAddressHelper.NormalizeMacAddress(new ConnectedPenDisplayInfo(connectedInfo)) : null;
                    if (string.Equals(mac, connectedMac, StringComparison.OrdinalIgnoreCase))
                    {
                        await _teiPenServiceWrapper.DisconnectFromPenAsync().ConfigureAwait(false);
                        await Dispatcher.InvokeAsync(() =>
                        {
                            ClearDiscoveredPens();
                            UpdateDeviceSectionVisibility(true);
                        });
                    }
                    await UpdatePairedPensAsync();
                    if (_appDataService != null)
                        await _appDataService.SyncPairedPensFromLibAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler beim Entfernen des gekoppelten Stifts {mac}: {ex.Message}");
            }
        }

        /// <summary>
        /// Behandelt Trennungsanfrage vom PenControl.
        /// </summary>
        private async void OnPenDisconnectRequested(object sender, object penInfo)
        {
            if (_teiPenServiceWrapper == null)
            {
                if (sender is PenControlBase pc)
                    await Dispatcher.InvokeAsync(() => pc.SetDisconnected());
                return;
            }

            bool success = false;
            try
            {
                success = await _teiPenServiceWrapper.DisconnectFromPenAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler beim Trennen der Verbindung: {ex.Message}");
            }

            await Dispatcher.InvokeAsync(() =>
            {
                if (sender is PenControlBase pc)
                {
                    pc.SetDisconnected();
                    if (success)
                    {
                        string disconnectedMac = MacAddressHelper.NormalizeMacAddress(penInfo);
                        if (!string.IsNullOrEmpty(disconnectedMac))
                        {
                            var sv = GetStartView();
                            if (sv?.PairedPenListContainer?.Items != null)
                            {
                                foreach (var item in sv.PairedPenListContainer.Items.Cast<object>().ToList())
                                {
                                    if (item is PairedPenControl pairedControl && pairedControl.PenInformation is PairedPenDisplayInfo p
                                        && string.Equals(MacAddressHelper.NormalizeMacAddress(p), disconnectedMac, StringComparison.OrdinalIgnoreCase))
                                    {
                                        pairedControl.SetDisconnected();
                                        break;
                                    }
                                }
                            }
                        }
                        if (pc is ConnectedPenControl)
                        {
                            var sv = GetStartView();
                            if (sv?.PenListContainer != null)
                            {
                                sv.PenListContainer.Items.Clear();
                                _ = UpdateDiscoveredPensAsync();
                            }
                        }
                    }
                }
            });
        }

        /// <summary>
        /// Entfernt ein Stift-Control aus der Liste basierend auf der MAC-Adresse.
        /// </summary>
        private void RemovePenControlFromList(PenInformation penInformation)
        {
            if (penInformation == null)
                return;

            string normalizedMacAddress = MacAddressHelper.NormalizeMacAddress((object)penInformation);
            if (string.IsNullOrEmpty(normalizedMacAddress))
                return;

            var sv = GetStartView();
            if (sv?.PenListContainer == null)
                return;
            var penListContainer = sv.PenListContainer;
            var contentAvailable = sv.ContentAvailable;

            for (int i = penListContainer.Items.Count - 1; i >= 0; i--)
            {
                if (penListContainer.Items[i] is PenControlBase penControl)
                {
                    string controlMac = MacAddressHelper.NormalizeMacAddress(penControl.PenInformation);
                    if (!string.IsNullOrEmpty(controlMac) && controlMac.Equals(normalizedMacAddress, StringComparison.Ordinal))
                    {
                        penListContainer.Items.RemoveAt(i);
                        UpdateStartButtonText();
                        break;
                    }
                }
            }

            if (contentAvailable != null)
                contentAvailable.IsHitTestVisible = penListContainer.Items.Count > 0;
        }

        /// <summary>
        /// Zeigt den verbundenen Stift in "Verfügbare Geräte" (explizit neues Control). Wird nach Verbindung aus "Gekoppelte Geräte" genutzt.
        /// Wenn GetConnectedPenInfo() noch null ist, wird nach kurzer Verzögerung erneut versucht. Wechselt den Tab nicht.
        /// </summary>
        private void ShowConnectedPenInListWhenReady()
        {
            var connectedInfo = _teiPenServiceWrapper?.GetConnectedPenInfo();
            if (connectedInfo != null)
            {
                ShowConnectedPenInList(connectedInfo, switchToAvailableTab: false);
                return;
            }
            var retryTimer = new DispatcherTimer(DispatcherPriority.Normal)
            {
                Interval = TimeSpan.FromMilliseconds(400)
            };
            retryTimer.Tick += (s, args) =>
            {
                retryTimer.Stop();
                retryTimer = null;
                var info = _teiPenServiceWrapper?.GetConnectedPenInfo();
                if (info != null)
                    ShowConnectedPenInList(info, switchToAvailableTab: false);
            };
            retryTimer.Start();
        }

        /// <summary>
        /// Zeigt nur den verbundenen Stift in der Liste (Tab "Verfügbare Geräte").
        /// </summary>
        /// <param name="switchToAvailableTab">True, um auf den Tab "Verfügbare Geräte" zu wechseln; false, um den aktuellen Tab beizubehalten.</param>
        private void ShowConnectedPenInList(PenConnectionInfoModel connectedInfo, bool switchToAvailableTab = true)
        {
            if (connectedInfo == null)
            {
                ClearDiscoveredPens();
                return;
            }

            if (switchToAvailableTab)
                GetStartView()?.SelectAvailableTab();

            var sv = GetStartView();
            if (sv?.PenListContainer == null)
                return;
            var penListContainer = sv.PenListContainer;
            var contentAvailable = sv.ContentAvailable;

            penListContainer.Items.Clear();
            var displayInfo = new ConnectedPenDisplayInfo(connectedInfo);
            var penControl = new ConnectedPenControl
            {
                PenInformation = displayInfo,
                Index = 0
            };
            penControl.ConnectionStateChanged += OnPenConnectionStateChanged;
            AttachConnectionHandlers(penControl);
            AttachPenPasswordHandlers(penControl);
            penListContainer.Items.Add(penControl);

            if (contentAvailable != null)
                contentAvailable.IsHitTestVisible = true;
            UpdateStartButtonText();
        }

        /// <summary>
        /// Leert die Liste der gefundenen Stifte.
        /// </summary>
        private void ClearDiscoveredPens()
        {
            var sv = GetStartView();
            if (sv?.PenListContainer != null)
            {
                sv.PenListContainer.Items.Clear();
                UpdateStartButtonText();
            }
            if (sv?.ContentAvailable != null)
                sv.ContentAvailable.IsHitTestVisible = false;
        }
    }
}

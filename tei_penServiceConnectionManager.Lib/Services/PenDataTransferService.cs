#nullable enable
using System;
using System.Threading.Tasks;
using Neosmartpen.Net;
using TeiPenServiceConnectionManager.Utilities;

namespace TeiPenServiceConnectionManager.Services
{
    /// <summary>
    /// Service für die Verwaltung der Datenübertragung zwischen Pen und Anwendung.
    /// Verarbeitet Real-time Dots und Offline-Daten-Downloads.
    /// </summary>
    public sealed class PenDataTransferService : IDisposable
    {
        private readonly PenController _penControllerConcrete;
        private readonly Func<bool> _isDisposed;
        private readonly Action<Action> _executeIfNotDisposed;

        // Event-Handler Delegates
        private readonly Action<IPenClient, DotReceivedEventArgs>? _onDotReceived;
        private readonly Action<IPenClient, OfflineDataListReceivedEventArgs>? _onOfflineDataListReceived;
        private readonly Action<IPenClient, OfflineStrokeReceivedEventArgs>? _onOfflineStrokeReceived;
        private readonly Action<IPenClient, SimpleResultEventArgs>? _onOfflineDownloadFinished;
        private readonly Action<IPenClient, object>? _onOfflineDataDownloadStarted;

        private bool _disposed;

        /// <summary>
        /// Initialisiert den PenDataTransferService.
        /// </summary>
        /// <param name="penControllerConcrete">PenController-Instanz für SDK-Aufrufe.</param>
        /// <param name="isDisposed">Delegate zum Prüfen, ob das Objekt disposed wurde.</param>
        /// <param name="executeIfNotDisposed">Delegate zum Ausführen von Aktionen, wenn nicht disposed.</param>
        /// <param name="onDotReceived">Optionaler Handler für DotReceived-Events.</param>
        /// <param name="onOfflineDataListReceived">Optionaler Handler für OfflineDataListReceived-Events.</param>
        /// <param name="onOfflineStrokeReceived">Optionaler Handler für OfflineStrokeReceived-Events.</param>
        /// <param name="onOfflineDownloadFinished">Optionaler Handler für OfflineDownloadFinished-Events.</param>
        /// <param name="onOfflineDataDownloadStarted">Optionaler Handler für OfflineDataDownloadStarted-Events.</param>
        public PenDataTransferService(
            PenController penControllerConcrete,
            Func<bool> isDisposed,
            Action<Action> executeIfNotDisposed,
            Action<IPenClient, DotReceivedEventArgs>? onDotReceived = null,
            Action<IPenClient, OfflineDataListReceivedEventArgs>? onOfflineDataListReceived = null,
            Action<IPenClient, OfflineStrokeReceivedEventArgs>? onOfflineStrokeReceived = null,
            Action<IPenClient, SimpleResultEventArgs>? onOfflineDownloadFinished = null,
            Action<IPenClient, object>? onOfflineDataDownloadStarted = null)
        {
            _penControllerConcrete = penControllerConcrete ?? throw new ArgumentNullException(nameof(penControllerConcrete));
            _isDisposed = isDisposed ?? throw new ArgumentNullException(nameof(isDisposed));
            _executeIfNotDisposed = executeIfNotDisposed ?? throw new ArgumentNullException(nameof(executeIfNotDisposed));
            _onDotReceived = onDotReceived;
            _onOfflineDataListReceived = onOfflineDataListReceived;
            _onOfflineStrokeReceived = onOfflineStrokeReceived;
            _onOfflineDownloadFinished = onOfflineDownloadFinished;
            _onOfflineDataDownloadStarted = onOfflineDataDownloadStarted;

            // Hinweis: Events werden NICHT hier abonniert, sondern über PenEventSubscriptionService.
            // Dieser Service stellt nur die Handler-Methoden bereit, die dann von PenEventSubscriptionService aufgerufen werden.
        }

        /// <summary>
        /// Handler für das DotReceived-Event des SDK.
        /// </summary>
        private void OnDotReceived(IPenClient sender, DotReceivedEventArgs args)
        {
            if (_isDisposed() || args == null)
            {
                return;
            }

            try
            {
                _executeIfNotDisposed(() =>
                {
                    _onDotReceived?.Invoke(sender, args);
                });
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler beim Verarbeiten des DotReceived-Events: {ex.Message}");
            }
        }

        /// <summary>
        /// Handler für das OfflineDataListReceived-Event des SDK.
        /// </summary>
        private void OnOfflineDataListReceived(IPenClient sender, OfflineDataListReceivedEventArgs args)
        {
            if (_isDisposed() || args == null)
            {
                return;
            }

            try
            {
                _executeIfNotDisposed(() =>
                {
                    _onOfflineDataListReceived?.Invoke(sender, args);
                });
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler beim Verarbeiten des OfflineDataListReceived-Events: {ex.Message}");
            }
        }

        /// <summary>
        /// Handler für das OfflineStrokeReceived-Event des SDK.
        /// </summary>
        private void OnOfflineStrokeReceived(IPenClient sender, OfflineStrokeReceivedEventArgs args)
        {
            if (_isDisposed() || args == null)
            {
                return;
            }

            try
            {
                _executeIfNotDisposed(() =>
                {
                    _onOfflineStrokeReceived?.Invoke(sender, args);
                });
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler beim Verarbeiten des OfflineStrokeReceived-Events: {ex.Message}");
            }
        }

        /// <summary>
        /// Handler für das OfflineDownloadFinished-Event des SDK.
        /// </summary>
        private void OnOfflineDownloadFinished(IPenClient sender, SimpleResultEventArgs args)
        {
            if (_isDisposed() || args == null)
            {
                return;
            }

            try
            {
                _executeIfNotDisposed(() =>
                {
                    _onOfflineDownloadFinished?.Invoke(sender, args);
                });
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler beim Verarbeiten des OfflineDownloadFinished-Events: {ex.Message}");
            }
        }

        /// <summary>
        /// Handler für das OfflineDataDownloadStarted-Event des SDK.
        /// </summary>
        private void OnOfflineDataDownloadStarted(IPenClient sender, object args)
        {
            if (_isDisposed())
            {
                return;
            }

            try
            {
                _executeIfNotDisposed(() =>
                {
                    _onOfflineDataDownloadStarted?.Invoke(sender, args);
                });
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler beim Verarbeiten des OfflineDataDownloadStarted-Events: {ex.Message}");
            }
        }

        /// <summary>
        /// Fordert die Liste der verfügbaren Offline-Daten vom Stift an.
        /// </summary>
        public void RequestOfflineDataList()
        {
            if (_isDisposed())
            {
                throw new ObjectDisposedException(nameof(PenDataTransferService));
            }

            try
            {
                _penControllerConcrete.RequestOfflineDataList();
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler beim Anfordern der Offline-Daten-Liste: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Fordert den Download von Offline-Daten für eine bestimmte Note an.
        /// </summary>
        /// <param name="section">Section Id.</param>
        /// <param name="owner">Owner Id.</param>
        /// <param name="note">Note Id.</param>
        /// <param name="deleteOnFinished">True, wenn die Daten nach dem Download gelöscht werden sollen.</param>
        /// <param name="pages">Optional: Array von Seiten-Nummern, die heruntergeladen werden sollen.</param>
        /// <returns>True, wenn die Anfrage erfolgreich war, false sonst.</returns>
        public bool RequestOfflineData(int section, int owner, int note, bool deleteOnFinished = true, int[]? pages = null)
        {
            if (_isDisposed())
            {
                throw new ObjectDisposedException(nameof(PenDataTransferService));
            }

            try
            {
                return _penControllerConcrete.RequestOfflineData(section, owner, note, deleteOnFinished, pages);
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler beim Anfordern der Offline-Daten: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Fordert den Download von Offline-Daten für mehrere Notes an.
        /// </summary>
        /// <param name="section">Section Id.</param>
        /// <param name="owner">Owner Id.</param>
        /// <param name="notes">Array von Note Ids.</param>
        /// <returns>True, wenn die Anfrage erfolgreich war, false sonst.</returns>
        public bool RequestOfflineData(int section, int owner, int[] notes)
        {
            if (_isDisposed())
            {
                throw new ObjectDisposedException(nameof(PenDataTransferService));
            }

            if (notes == null || notes.Length == 0)
            {
                throw new ArgumentException("Notes-Array darf nicht null oder leer sein.", nameof(notes));
            }

            try
            {
                return _penControllerConcrete.RequestOfflineData(section, owner, notes);
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler beim Anfordern der Offline-Daten: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Entfernt Offline-Daten vom Stift.
        /// </summary>
        /// <param name="section">Section Id.</param>
        /// <param name="owner">Owner Id.</param>
        /// <param name="notes">Array von Note Ids, die entfernt werden sollen.</param>
        public void RequestRemoveOfflineData(int section, int owner, int[] notes)
        {
            if (_isDisposed())
            {
                throw new ObjectDisposedException(nameof(PenDataTransferService));
            }

            if (notes == null || notes.Length == 0)
            {
                throw new ArgumentException("Notes-Array darf nicht null oder leer sein.", nameof(notes));
            }

            try
            {
                _penControllerConcrete.RequestRemoveOfflineData(section, owner, notes);
            }
            catch (Exception ex)
            {
                ThreadSafeConsole.WriteLine($"Fehler beim Entfernen der Offline-Daten: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Implementiert das Dispose Pattern und gibt alle Ressourcen frei.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            // Keine Event-Unsubscription nötig, da Events über PenEventSubscriptionService verwaltet werden
            _disposed = true;
        }
    }
}

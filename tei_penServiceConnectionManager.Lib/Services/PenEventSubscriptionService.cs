using System;
using Neosmartpen.Net;
using Neosmartpen.Net.Bluetooth;

#nullable enable

namespace TeiPenServiceConnectionManager.Services
{
    /// <summary>
    /// Service für die Verwaltung von Event Subscriptions für Pen-Events.
    /// Verwaltet Subscribe/Unsubscribe und delegiert Event-Handler an spezialisierte Services.
    /// </summary>
    public sealed class PenEventSubscriptionService : IDisposable
    {
        private readonly GenericBluetoothPenClient _penClient;
        private readonly PenController _penControllerConcrete;

        // Event Handler Delegates von spezialisierten Services
        private readonly Action<IPenClient, ConnectedEventArgs>? _onConnected;
        private readonly Action<IPenClient, object>? _onDisconnected;
        private readonly Action<IPenClient, PasswordRequestedEventArgs>? _onPasswordRequested;
        private readonly Action<IPenClient, SimpleResultEventArgs>? _onPasswordChanged;
        private readonly Action<IPenClient, object>? _onAuthenticated;
        private readonly Action<IPenClient, DotReceivedEventArgs>? _onDotReceived;
        private readonly Action<IPenClient, PenStatusReceivedEventArgs>? _onPenStatusReceived;
        private readonly Action<IPenClient, PenInformation>? _onPenFound;
        private readonly Action<IPenClient, PenUpdateInformation>? _onPenUpdated;
        private readonly Action<IPenClient, Windows.Devices.Bluetooth.BluetoothError>? _onSearchStopped;
        private readonly Action<IPenClient, OfflineDataListReceivedEventArgs>? _onOfflineDataListReceived;
        private readonly Action<IPenClient, OfflineStrokeReceivedEventArgs>? _onOfflineStrokeReceived;
        private readonly Action<IPenClient, SimpleResultEventArgs>? _onOfflineDownloadFinished;
        private readonly Action<IPenClient, object>? _onOfflineDataDownloadStarted;

        private bool _disposed;

        /// <summary>
        /// Initialisiert den PenEventSubscriptionService.
        /// </summary>
        public PenEventSubscriptionService(
            GenericBluetoothPenClient penClient,
            PenController penControllerConcrete,
            Action<IPenClient, ConnectedEventArgs>? onConnected = null,
            Action<IPenClient, object>? onDisconnected = null,
            Action<IPenClient, PasswordRequestedEventArgs>? onPasswordRequested = null,
            Action<IPenClient, SimpleResultEventArgs>? onPasswordChanged = null,
            Action<IPenClient, object>? onAuthenticated = null,
            Action<IPenClient, DotReceivedEventArgs>? onDotReceived = null,
            Action<IPenClient, PenStatusReceivedEventArgs>? onPenStatusReceived = null,
            Action<IPenClient, PenInformation>? onPenFound = null,
            Action<IPenClient, PenUpdateInformation>? onPenUpdated = null,
            Action<IPenClient, Windows.Devices.Bluetooth.BluetoothError>? onSearchStopped = null,
            Action<IPenClient, OfflineDataListReceivedEventArgs>? onOfflineDataListReceived = null,
            Action<IPenClient, OfflineStrokeReceivedEventArgs>? onOfflineStrokeReceived = null,
            Action<IPenClient, SimpleResultEventArgs>? onOfflineDownloadFinished = null,
            Action<IPenClient, object>? onOfflineDataDownloadStarted = null)
        {
            _penClient = penClient ?? throw new ArgumentNullException(nameof(penClient));
            _penControllerConcrete = penControllerConcrete ?? throw new ArgumentNullException(nameof(penControllerConcrete));
            _onConnected = onConnected;
            _onDisconnected = onDisconnected;
            _onPasswordRequested = onPasswordRequested;
            _onPasswordChanged = onPasswordChanged;
            _onAuthenticated = onAuthenticated;
            _onDotReceived = onDotReceived;
            _onPenStatusReceived = onPenStatusReceived;
            _onPenFound = onPenFound;
            _onPenUpdated = onPenUpdated;
            _onSearchStopped = onSearchStopped;
            _onOfflineDataListReceived = onOfflineDataListReceived;
            _onOfflineStrokeReceived = onOfflineStrokeReceived;
            _onOfflineDownloadFinished = onOfflineDownloadFinished;
            _onOfflineDataDownloadStarted = onOfflineDataDownloadStarted;

            SubscribeToEvents();
        }

        /// <summary>
        /// Abonniert die Pen-Events des SDK.
        /// </summary>
        private void SubscribeToEvents()
        {
            _penClient.onAddPenController += OnPenFound;
            _penClient.onUpdatePenController += OnPenUpdated;
            _penClient.onStopSearch += OnSearchStopped;

            _penControllerConcrete.Connected += OnConnected;
            _penControllerConcrete.Disconnected += OnDisconnected;
            _penControllerConcrete.PasswordRequested += OnPasswordRequested;
            _penControllerConcrete.PasswordChanged += OnPasswordChanged;
            _penControllerConcrete.Authenticated += OnAuthenticated;
            _penControllerConcrete.DotReceived += OnDotReceived;
            _penControllerConcrete.PenStatusReceived += OnPenStatusReceived;
            _penControllerConcrete.OfflineDataListReceived += OnOfflineDataListReceived;
            _penControllerConcrete.OfflineStrokeReceived += OnOfflineStrokeReceived;
            _penControllerConcrete.OfflineDownloadFinished += OnOfflineDownloadFinished;
            _penControllerConcrete.OfflineDataDownloadStarted += OnOfflineDataDownloadStarted;
        }

        /// <summary>
        /// Deabonniert die Pen-Events des SDK.
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            _penClient.onAddPenController -= OnPenFound;
            _penClient.onUpdatePenController -= OnPenUpdated;
            _penClient.onStopSearch -= OnSearchStopped;

            _penControllerConcrete.Connected -= OnConnected;
            _penControllerConcrete.Disconnected -= OnDisconnected;
            _penControllerConcrete.PasswordRequested -= OnPasswordRequested;
            _penControllerConcrete.PasswordChanged -= OnPasswordChanged;
            _penControllerConcrete.Authenticated -= OnAuthenticated;
            _penControllerConcrete.DotReceived -= OnDotReceived;
            _penControllerConcrete.PenStatusReceived -= OnPenStatusReceived;
            _penControllerConcrete.OfflineDataListReceived -= OnOfflineDataListReceived;
            _penControllerConcrete.OfflineStrokeReceived -= OnOfflineStrokeReceived;
            _penControllerConcrete.OfflineDownloadFinished -= OnOfflineDownloadFinished;
            _penControllerConcrete.OfflineDataDownloadStarted -= OnOfflineDataDownloadStarted;
        }

        /// <summary>
        /// Handler für das Connected-Event des SDK.
        /// </summary>
        private void OnConnected(IPenClient sender, ConnectedEventArgs args)
        {
            _onConnected?.Invoke(sender, args);
        }

        /// <summary>
        /// Handler für das Disconnected-Event des SDK.
        /// </summary>
        private void OnDisconnected(IPenClient sender, object args)
        {
            _onDisconnected?.Invoke(sender, args);
        }

        /// <summary>
        /// Handler für das PasswordRequested-Event des SDK.
        /// </summary>
        private void OnPasswordRequested(IPenClient sender, PasswordRequestedEventArgs args)
        {
            _onPasswordRequested?.Invoke(sender, args);
        }

        /// <summary>
        /// Handler für das PasswordChanged-Event des SDK.
        /// </summary>
        private void OnPasswordChanged(IPenClient sender, SimpleResultEventArgs args)
        {
            _onPasswordChanged?.Invoke(sender, args);
        }

        /// <summary>
        /// Handler für das Authenticated-Event des SDK.
        /// </summary>
        private void OnAuthenticated(IPenClient sender, object args)
        {
            _onAuthenticated?.Invoke(sender, args);
        }

        /// <summary>
        /// Handler für das DotReceived-Event des SDK.
        /// </summary>
        private void OnDotReceived(IPenClient sender, DotReceivedEventArgs args)
        {
            if (args?.Dot != null)
            {
                System.Diagnostics.Debug.WriteLine($"PenEventSubscriptionService: DotReceived Event gefeuert - Section: {args.Dot.Section}, Owner: {args.Dot.Owner}, Note: {args.Dot.Note}, Page: {args.Dot.Page}, Type: {args.Dot.DotType}");
            }
            _onDotReceived?.Invoke(sender, args);
        }

        /// <summary>
        /// Handler für das PenStatusReceived-Event des SDK.
        /// </summary>
        private void OnPenStatusReceived(IPenClient sender, PenStatusReceivedEventArgs args)
        {
            _onPenStatusReceived?.Invoke(sender, args);
        }

        /// <summary>
        /// Handler für das PenFound-Event des SDK.
        /// </summary>
        private void OnPenFound(IPenClient sender, PenInformation args)
        {
            _onPenFound?.Invoke(sender, args);
        }

        /// <summary>
        /// Handler für das PenUpdated-Event des SDK.
        /// </summary>
        private void OnPenUpdated(IPenClient sender, PenUpdateInformation args)
        {
            _onPenUpdated?.Invoke(sender, args);
        }

        /// <summary>
        /// Handler für das SearchStopped-Event des SDK.
        /// </summary>
        private void OnSearchStopped(IPenClient sender, Windows.Devices.Bluetooth.BluetoothError args)
        {
            _onSearchStopped?.Invoke(sender, args);
        }

        /// <summary>
        /// Handler für das OfflineDataListReceived-Event des SDK.
        /// </summary>
        private void OnOfflineDataListReceived(IPenClient sender, OfflineDataListReceivedEventArgs args)
        {
            _onOfflineDataListReceived?.Invoke(sender, args);
        }

        /// <summary>
        /// Handler für das OfflineStrokeReceived-Event des SDK.
        /// </summary>
        private void OnOfflineStrokeReceived(IPenClient sender, OfflineStrokeReceivedEventArgs args)
        {
            _onOfflineStrokeReceived?.Invoke(sender, args);
        }

        /// <summary>
        /// Handler für das OfflineDownloadFinished-Event des SDK.
        /// </summary>
        private void OnOfflineDownloadFinished(IPenClient sender, SimpleResultEventArgs args)
        {
            _onOfflineDownloadFinished?.Invoke(sender, args);
        }

        /// <summary>
        /// Handler für das OfflineDataDownloadStarted-Event des SDK.
        /// </summary>
        private void OnOfflineDataDownloadStarted(IPenClient sender, object args)
        {
            _onOfflineDataDownloadStarted?.Invoke(sender, args);
        }

        /// <summary>
        /// Implementiert das Dispose Pattern und deabonniert die Events.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            UnsubscribeFromEvents();
            _disposed = true;
        }
    }
}

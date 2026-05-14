using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace tei_penService_ui.Controls
{
    /// <summary>
    /// Basisklasse für StandardPenControl, ConnectedPenControl und PairedPenControl.
    /// Gemeinsame API (PenInformation, Index, Events, SetConnectionSucceeded/SetConnectionFailed/SetDisconnected, RefreshHoverState)
    /// und Hilfsmethoden (ApplyControlBorderAppearance, TryFindBrush).
    /// </summary>
    public abstract class PenControlBase : UserControl
    {
        /// <summary>
        /// DependencyProperty für PenInformation (PenInformation, ConnectedPenDisplayInfo oder PairedPenDisplayInfo).
        /// </summary>
        public static readonly DependencyProperty PenInformationProperty =
            DependencyProperty.Register(
                nameof(PenInformation),
                typeof(object),
                typeof(PenControlBase),
                new PropertyMetadata(null, OnPenInformationChanged));

        private static void OnPenInformationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PenControlBase control)
                control.OnPenInformationChanged(e.OldValue, e.NewValue);
        }

        /// <summary>
        /// Wird aufgerufen, wenn sich PenInformation geändert hat. Unterklassen können die Anzeige aktualisieren (z. B. RSSI/Reichweite).
        /// </summary>
        protected virtual void OnPenInformationChanged(object oldValue, object newValue) { }

        /// <summary>
        /// DependencyProperty für den Index (z. B. für Farbrotation).
        /// </summary>
        public static readonly DependencyProperty IndexProperty =
            DependencyProperty.Register(
                nameof(Index),
                typeof(int),
                typeof(PenControlBase),
                new PropertyMetadata(0));

        /// <summary>
        /// Anzeige-Daten des Stifts.
        /// </summary>
        public object PenInformation
        {
            get => GetValue(PenInformationProperty);
            set => SetValue(PenInformationProperty, value);
        }

        /// <summary>
        /// Index des Stifts in der Liste.
        /// </summary>
        public int Index
        {
            get => (int)GetValue(IndexProperty);
            set => SetValue(IndexProperty, value);
        }

        /// <summary>
        /// Gibt an, ob dieser Stift verbunden ist. Von Unterklassen überschrieben.
        /// </summary>
        public abstract bool IsConnected { get; }

        /// <summary>
        /// Wird ausgelöst, wenn sich der Verbindungszustand ändert (verbunden/getrennt).
        /// </summary>
        public event EventHandler<bool> ConnectionStateChanged;

        /// <summary>
        /// Wird ausgelöst, wenn der Benutzer "Verbinden" geklickt hat.
        /// </summary>
        public event EventHandler<object> ConnectRequested;

        /// <summary>
        /// Wird ausgelöst, wenn der Benutzer "Trennen" geklickt hat.
        /// </summary>
        public event EventHandler<object> DisconnectRequested;

        /// <summary>
        /// Wird ausgelöst, wenn der Benutzer "Entfernen" geklickt hat (nur PairedPenControl).
        /// </summary>
        public event EventHandler<object> RemoveRequested;

        /// <summary>
        /// Wird ausgelöst, wenn der Benutzer im Passwortfeld OK wählt (Wert = eingegebenes Passwort).
        /// </summary>
        public event EventHandler<string> PasswordSubmitted;

        /// <summary>
        /// Löst <see cref="PasswordSubmitted"/> aus.
        /// </summary>
        protected void RaisePasswordSubmitted(string password)
        {
            PasswordSubmitted?.Invoke(this, password ?? string.Empty);
        }

        /// <summary>
        /// Löst ConnectionStateChanged aus. Geschützte Methode für Unterklassen.
        /// </summary>
        protected void RaiseConnectionStateChanged(bool isConnected)
        {
            ConnectionStateChanged?.Invoke(this, isConnected);
        }

        /// <summary>
        /// Löst ConnectRequested aus.
        /// </summary>
        protected void RaiseConnectRequested()
        {
            if (PenInformation != null)
                ConnectRequested?.Invoke(this, PenInformation);
        }

        /// <summary>
        /// Löst DisconnectRequested aus.
        /// </summary>
        protected void RaiseDisconnectRequested()
        {
            if (PenInformation != null)
                DisconnectRequested?.Invoke(this, PenInformation);
        }

        /// <summary>
        /// Löst RemoveRequested aus.
        /// </summary>
        protected void RaiseRemoveRequested()
        {
            if (PenInformation != null)
                RemoveRequested?.Invoke(this, PenInformation);
        }

        /// <summary>
        /// Wird von MainWindow aufgerufen, wenn die Verbindung erfolgreich hergestellt wurde.
        /// Standard: keine Operation (StandardPenControl wird ersetzt; ConnectedPenControl ist von vornherein verbunden).
        /// </summary>
        public virtual void SetConnectionSucceeded() { }

        /// <summary>
        /// Wird von MainWindow aufgerufen, wenn die Verbindung fehlgeschlagen ist.
        /// </summary>
        public virtual void SetConnectionFailed() { }

        /// <summary>
        /// Wird von MainWindow aufgerufen, wenn die Verbindung getrennt wurde.
        /// Standard: keine Operation (ConnectedPenControl wird aus der Liste entfernt).
        /// </summary>
        public virtual void SetDisconnected() { }

        /// <summary>
        /// Stellt den Hover-Zustand wieder her (z. B. nach Discovery-Update). Standard: keine Operation.
        /// </summary>
        public virtual void RefreshHoverState() { }

        /// <summary>
        /// Setzt Hintergrund und Rand des äußeren Borders (Name muss "PenControlBorder" sein).
        /// </summary>
        protected virtual void ApplyControlBorderAppearance(bool isHover)
        {
            var border = FindName("PenControlBorder") as Border;
            if (border == null)
                return;
            string bgKey = isHover ? "TitleBarHoverBrush" : "TitleBarBrush";
            var brush = TryFindBrush(bgKey);
            if (brush != null)
                border.Background = brush;
            border.BorderBrush = Brushes.Transparent;
        }

        /// <summary>
        /// Sucht einen Brush in den Application-Resources.
        /// </summary>
        protected static SolidColorBrush TryFindBrush(string key)
        {
            return Application.Current?.TryFindResource(key) as SolidColorBrush;
        }
    }
}

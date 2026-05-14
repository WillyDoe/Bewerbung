using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace tei_penService_ui.Controls
{
    /// <summary>
    /// Anzeige-Modus: Verfügbare Geräte (RSSI + Verbinden/Passwort/Trennen), Gekoppelte Geräte (RSSI/Verbunden/nicht in Reichweite + Entfernen/Verbinden/Trennen), oder nur Verbunden (Verbunden + Trennen).
    /// </summary>
    public enum PenControlDisplayMode
    {
        Available,
        Paired,
        Connected
    }

    /// <summary>
    /// Einheitliches Stift-Control. Name und MAC fest; Sektion 3 und Hover-Verhalten werden zur Laufzeit anhand DisplayMode und Verbindungszustand gesteuert.
    /// </summary>
    public partial class PenControl : PenControlBase
    {
        private const double StandardRowHeight = 40;
        private const double PasswordRowMinHeight = 56;

        private bool _isConnectMode;
        private bool _showPasswordInput;
        private bool _isPasswordError;
        private bool _isHovering;
        private bool _isConnectedForAvailable;
        private bool _isConnectedForPaired;
        private DispatcherTimer _connectDelayTimer;

        /// <summary>
        /// Steuert, ob das Control sich wie "Verfügbare Geräte", "Gekoppelte Geräte" oder "Verbunden" (nur ein verbundener Stift) verhält.
        /// </summary>
        public PenControlDisplayMode DisplayMode { get; set; }

        public override bool IsConnected =>
            DisplayMode == PenControlDisplayMode.Connected ||
            (DisplayMode == PenControlDisplayMode.Available && _isConnectedForAvailable) ||
            (DisplayMode == PenControlDisplayMode.Paired && _isConnectedForPaired);

        public PenControl()
        {
            InitializeComponent();
            MouseEnter += PenControl_MouseEnter;
            MouseLeave += PenControl_MouseLeave;
            Loaded += PenControl_Loaded;
        }

        protected override void OnPenInformationChanged(object oldValue, object newValue)
        {
            base.OnPenInformationChanged(oldValue, newValue);
            if (DisplayMode == PenControlDisplayMode.Paired)
                UpdateSection3Visibility();
        }

        private void PenControl_Loaded(object sender, RoutedEventArgs e)
        {
            var passwordBox = FindName("PenPasswordBox") as UIElement;
            var passwordFieldBorder = FindName("PasswordFieldBorder") as Border;
            if (passwordBox != null && passwordFieldBorder != null)
            {
                passwordBox.GotFocus += PasswordBox_GotFocus;
                passwordBox.LostFocus += PasswordBox_LostFocus;
            }
            if (!_showPasswordInput)
                ApplyStandardRowHeight();
            UpdateSection3Visibility();
            ApplyNormalViewTextColors(IsMouseOver);
            ApplyControlBorderAppearance(IsMouseOver);
            if (DisplayMode == PenControlDisplayMode.Connected)
                RaiseConnectionStateChanged(true);
        }

        protected override void ApplyControlBorderAppearance(bool isHover)
        {
            base.ApplyControlBorderAppearance(isHover);
            var border = FindName("PenControlBorder") as Border;
            if (border == null)
                return;
            bool useConnectedColors = DisplayMode == PenControlDisplayMode.Connected ||
                (DisplayMode == PenControlDisplayMode.Paired && _isConnectedForPaired);
            if (useConnectedColors)
            {
                var teiBlue = TryFindBrush("TeiBlueBrush");
                if (teiBlue != null)
                    border.BorderBrush = teiBlue;
                border.BorderThickness = new Thickness(1);
            }
            else
            {
                border.BorderBrush = Brushes.Transparent;
            }
        }

        private void PenControl_MouseEnter(object sender, MouseEventArgs e)
        {
            _isHovering = true;
            ApplyControlBorderAppearance(isHover: true);
            ApplyNormalViewTextColors(useHoverColors: true);
            UpdateViewsVisibility();
        }

        private void PenControl_MouseLeave(object sender, MouseEventArgs e)
        {
            _isHovering = false;
            ApplyControlBorderAppearance(isHover: false);
            ApplyNormalViewTextColors(useHoverColors: false);
            UpdateViewsVisibility();
        }

        private void PenControlBorder_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DisplayMode == PenControlDisplayMode.Available && !_isConnectMode)
            {
                _isConnectMode = true;
                UpdateViewsVisibility();
            }
        }

        /// <summary>NormalView / ConnectView / HoverView und Sektion-3-Inhalte je nach DisplayMode und Zustand aktualisieren.</summary>
        private void UpdateViewsVisibility()
        {
            var normalView = FindName("NormalView") as FrameworkElement;
            var connectView = FindName("ConnectView") as FrameworkElement;
            var hoverView = FindName("HoverView") as FrameworkElement;
            var connectLoadingButton = FindName("ConnectLoadingButton") as FrameworkElement;
            var disconnectLoadingButton = FindName("DisconnectLoadingButton") as FrameworkElement;
            var hoverRemoveButton = FindName("HoverRemoveLoadingButton") as FrameworkElement;
            var hoverDisconnectButton = FindName("HoverDisconnectButton") as FrameworkElement;
            var hoverConnectButton = FindName("HoverConnectLoadingButton") as FrameworkElement;
            var hoverDisconnectLoadingButton = FindName("HoverDisconnectLoadingButton") as FrameworkElement;
            var passwordPanel = FindName("PasswordInputPanel") as FrameworkElement;

            // Passwort erforderlich (SDK): ConnectView mit Passwortfeld – auch für Paired/Connected, sonst ist das Feld nicht sichtbar.
            if (_showPasswordInput && DisplayMode != PenControlDisplayMode.Available)
            {
                if (normalView != null) normalView.Visibility = Visibility.Collapsed;
                if (connectView != null) connectView.Visibility = Visibility.Visible;
                if (hoverView != null) hoverView.Visibility = Visibility.Collapsed;
                if (connectLoadingButton is LoadingButton clb)
                {
                    clb.IsLoading = false;
                    clb.Visibility = Visibility.Collapsed;
                }
                if (passwordPanel != null) passwordPanel.Visibility = Visibility.Visible;
                if (disconnectLoadingButton != null)
                    disconnectLoadingButton.Visibility = _isConnectedForAvailable || _isConnectedForPaired ? Visibility.Visible : Visibility.Collapsed;
                var hoverConnectLb = FindName("HoverConnectLoadingButton") as LoadingButton;
                if (hoverConnectLb != null) hoverConnectLb.IsLoading = false;
                ApplyControlBorderAppearance(IsMouseOver);
                return;
            }

            if (DisplayMode == PenControlDisplayMode.Available)
            {
                bool showConnectView = _isConnectMode || _isHovering || _showPasswordInput;
                if (normalView != null) normalView.Visibility = showConnectView ? Visibility.Collapsed : Visibility.Visible;
                if (connectView != null) connectView.Visibility = showConnectView ? Visibility.Visible : Visibility.Collapsed;
                if (connectLoadingButton != null && disconnectLoadingButton != null)
                {
                    if (_showPasswordInput)
                    {
                        if (connectLoadingButton is LoadingButton clb)
                        {
                            clb.IsLoading = false;
                            clb.Visibility = Visibility.Collapsed;
                        }
                        if (passwordPanel != null) passwordPanel.Visibility = Visibility.Visible;
                        disconnectLoadingButton.Visibility = _isConnectedForAvailable ? Visibility.Visible : Visibility.Collapsed;
                    }
                    else
                    {
                        if (passwordPanel != null) passwordPanel.Visibility = Visibility.Collapsed;
                        connectLoadingButton.Visibility = _isConnectedForAvailable ? Visibility.Collapsed : Visibility.Visible;
                        disconnectLoadingButton.Visibility = _isConnectedForAvailable ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
                if (hoverView != null) hoverView.Visibility = Visibility.Collapsed;
            }
            else if (DisplayMode == PenControlDisplayMode.Paired)
            {
                if (normalView != null) normalView.Visibility = _isHovering ? Visibility.Collapsed : Visibility.Visible;
                if (connectView != null) connectView.Visibility = Visibility.Collapsed;
                if (hoverView != null) hoverView.Visibility = _isHovering ? Visibility.Visible : Visibility.Collapsed;
                bool inRange = GetIsInRange();
                if (hoverRemoveButton != null) hoverRemoveButton.Visibility = Visibility.Visible;
                if (hoverDisconnectButton != null) hoverDisconnectButton.Visibility = Visibility.Collapsed;
                if (hoverConnectButton != null)
                    hoverConnectButton.Visibility = _isHovering && inRange && !_isConnectedForPaired ? Visibility.Visible : Visibility.Collapsed;
                if (hoverDisconnectLoadingButton != null)
                    hoverDisconnectLoadingButton.Visibility = _isHovering && _isConnectedForPaired ? Visibility.Visible : Visibility.Collapsed;
            }
            else // Connected
            {
                if (normalView != null) normalView.Visibility = _isHovering ? Visibility.Collapsed : Visibility.Visible;
                if (connectView != null) connectView.Visibility = Visibility.Collapsed;
                if (hoverView != null) hoverView.Visibility = _isHovering ? Visibility.Visible : Visibility.Collapsed;
                if (hoverRemoveButton != null) hoverRemoveButton.Visibility = Visibility.Collapsed;
                if (hoverDisconnectButton != null) hoverDisconnectButton.Visibility = _isHovering ? Visibility.Visible : Visibility.Collapsed;
                if (hoverConnectButton != null) hoverConnectButton.Visibility = Visibility.Collapsed;
                if (hoverDisconnectLoadingButton != null) hoverDisconnectLoadingButton.Visibility = Visibility.Collapsed;
            }

            ApplyControlBorderAppearance(IsMouseOver);
        }

        private void UpdateSection3Visibility()
        {
            var rssiBarPanel = FindName("RssiBarPanel") as FrameworkElement;
            var verbundenText = FindName("VerbundenTextBlock") as FrameworkElement;
            var nichtInReichweiteText = FindName("NichtInReichweiteTextBlock") as FrameworkElement;
            bool inRange = GetIsInRange();

            if (DisplayMode == PenControlDisplayMode.Connected)
            {
                SetVisibility(rssiBarPanel, Visibility.Collapsed);
                SetVisibility(verbundenText, Visibility.Visible);
                SetVisibility(nichtInReichweiteText, Visibility.Collapsed);
                return;
            }

            if (DisplayMode == PenControlDisplayMode.Paired)
            {
                if (_isConnectedForPaired)
                {
                    SetVisibility(rssiBarPanel, Visibility.Collapsed);
                    SetVisibility(verbundenText, Visibility.Visible);
                    SetVisibility(nichtInReichweiteText, Visibility.Collapsed);
                }
                else if (inRange)
                {
                    SetVisibility(rssiBarPanel, Visibility.Visible);
                    SetVisibility(verbundenText, Visibility.Collapsed);
                    SetVisibility(nichtInReichweiteText, Visibility.Collapsed);
                }
                else
                {
                    SetVisibility(rssiBarPanel, Visibility.Collapsed);
                    SetVisibility(verbundenText, Visibility.Collapsed);
                    SetVisibility(nichtInReichweiteText, Visibility.Visible);
                }
                return;
            }

            // Available: Section3 = RSSI only
            SetVisibility(rssiBarPanel, Visibility.Visible);
            SetVisibility(verbundenText, Visibility.Collapsed);
            SetVisibility(nichtInReichweiteText, Visibility.Collapsed);
        }

        private static void SetVisibility(UIElement element, Visibility visibility)
        {
            if (element != null)
                element.Visibility = visibility;
        }

        private bool GetIsInRange()
        {
            return (PenInformation as Models.PairedPenDisplayInfo)?.IsInRange ?? false;
        }

        private void ApplyNormalViewTextColors(bool useHoverColors)
        {
            var nameTextBlock = FindName("PenNameTextBlock") as TextBlock;
            var macTextBlock = FindName("MacAddressTextBlock") as TextBlock;
            var rssiTextBlock = FindName("RssiTextBlock") as TextBlock;
            var verbundenTextBlock = FindName("VerbundenTextBlock") as TextBlock;
            var nichtInReichweiteTextBlock = FindName("NichtInReichweiteTextBlock") as TextBlock;

            // Connected-Modus oder Paired mit verbundenem Stift: Name=TeiCyan, MAC=TeiYellow, Verbunden=TeiGreen
            bool useConnectedColors = DisplayMode == PenControlDisplayMode.Connected ||
                (DisplayMode == PenControlDisplayMode.Paired && _isConnectedForPaired);

            if (useConnectedColors)
            {
                var teiGreen = TryFindBrush("TeiGreenBrush");
                var teiYellow = TryFindBrush("TeiYellowBrush");
                var teiCyan = TryFindBrush("TeiCyanBrush");
                if (nameTextBlock != null && teiCyan != null) nameTextBlock.Foreground = teiCyan;
                if (macTextBlock != null && teiYellow != null) macTextBlock.Foreground = teiYellow;
                if (verbundenTextBlock != null && teiGreen != null) verbundenTextBlock.Foreground = teiGreen;
                if (rssiTextBlock != null && teiCyan != null) rssiTextBlock.Foreground = teiCyan;
                if (nichtInReichweiteTextBlock != null && teiCyan != null) nichtInReichweiteTextBlock.Foreground = teiCyan;
            }
            else if (useHoverColors)
            {
                var teiGreen = TryFindBrush("TeiGreenBrush");
                var teiBlue = TryFindBrush("TeiBlueBrush");
                var teiCyan = TryFindBrush("TeiCyanBrush");
                if (nameTextBlock != null && teiGreen != null) nameTextBlock.Foreground = teiGreen;
                if (macTextBlock != null && teiBlue != null) macTextBlock.Foreground = teiBlue;
                if (rssiTextBlock != null && teiCyan != null) rssiTextBlock.Foreground = teiCyan;
                if (verbundenTextBlock != null && teiCyan != null) verbundenTextBlock.Foreground = teiCyan;
                if (nichtInReichweiteTextBlock != null && teiCyan != null) nichtInReichweiteTextBlock.Foreground = teiCyan;
            }
            else
            {
                var textBrush = TryFindBrush("TextBrush");
                var textSecondary = TryFindBrush("TextSecondaryBrush");
                if (nameTextBlock != null && textBrush != null) nameTextBlock.Foreground = textBrush;
                if (macTextBlock != null && textBrush != null) macTextBlock.Foreground = textBrush;
                if (rssiTextBlock != null && textBrush != null) rssiTextBlock.Foreground = textBrush;
                if (verbundenTextBlock != null && textBrush != null) verbundenTextBlock.Foreground = textBrush;
                if (nichtInReichweiteTextBlock != null)
                    nichtInReichweiteTextBlock.Foreground = textSecondary ?? textBrush;
            }
        }

        // --- Available: Connect / Password / Disconnect ---
        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (_showPasswordInput || PenInformation == null) return;
            var connectLoadingButton = FindName("ConnectLoadingButton") as LoadingButton;
            var passwordPanel = FindName("PasswordInputPanel") as FrameworkElement;
            if (connectLoadingButton != null) connectLoadingButton.IsLoading = true;
            if (passwordPanel != null) passwordPanel.Visibility = Visibility.Collapsed;
            RaiseConnectRequested();
        }

        private void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            RaiseDisconnectRequested();
        }

        public override void SetConnectionSucceeded()
        {
            if (DisplayMode == PenControlDisplayMode.Available)
            {
                _isConnectedForAvailable = true;
                var connectLoadingButton = FindName("ConnectLoadingButton") as LoadingButton;
                if (connectLoadingButton != null)
                {
                    connectLoadingButton.IsLoading = false;
                    connectLoadingButton.Visibility = Visibility.Visible;
                }
            }
            else if (DisplayMode == PenControlDisplayMode.Paired)
            {
                _isConnectedForPaired = true;
                var hoverConnectLoadingButton = FindName("HoverConnectLoadingButton") as LoadingButton;
                if (hoverConnectLoadingButton != null) hoverConnectLoadingButton.IsLoading = false;
                UpdateSection3Visibility();
                RaiseConnectionStateChanged(true);
            }
            UpdateViewsVisibility();
            ApplyNormalViewTextColors(IsMouseOver);
        }

        public override void SetConnectionFailed()
        {
            var connectLoadingButton = FindName("ConnectLoadingButton") as LoadingButton;
            if (connectLoadingButton != null)
            {
                connectLoadingButton.IsLoading = false;
                connectLoadingButton.Visibility = Visibility.Visible;
            }
            var hoverConnectLoadingButton = FindName("HoverConnectLoadingButton") as LoadingButton;
            if (hoverConnectLoadingButton != null) hoverConnectLoadingButton.IsLoading = false;
            ClearPasswordUiIfVisible();
        }

        public override void SetDisconnected()
        {
            if (DisplayMode == PenControlDisplayMode.Paired)
            {
                _isConnectedForPaired = false;
                UpdateSection3Visibility();
                RaiseConnectionStateChanged(false);
            }
            ClearPasswordUiIfVisible();
            UpdateViewsVisibility();
            ApplyNormalViewTextColors(IsMouseOver);
        }

        /// <summary>
        /// Blendet die Passwortzeile aus und stellt die Standard-Zeilenhöhe (40 px) wieder her.
        /// </summary>
        private void ClearPasswordUiIfVisible()
        {
            if (!_showPasswordInput)
                return;
            _showPasswordInput = false;
            var passwordPanel = FindName("PasswordInputPanel") as FrameworkElement;
            if (passwordPanel != null)
                passwordPanel.Visibility = Visibility.Collapsed;
            var passwordBox = FindName("PenPasswordBox") as PasswordBox;
            if (passwordBox != null)
                passwordBox.Password = string.Empty;
            SetPasswordInputError(false);
            ApplyStandardRowHeight();
        }

        public override void RefreshHoverState()
        {
            if (!IsMouseOver) return;
            ApplyControlBorderAppearance(isHover: true);
            ApplyNormalViewTextColors(useHoverColors: true);
            _isHovering = true;
            UpdateViewsVisibility();
        }

        private void ApplyStandardRowHeight()
        {
            Height = StandardRowHeight;
            MinHeight = StandardRowHeight;
            MaxHeight = StandardRowHeight;
        }

        private void ApplyPasswordRowHeight()
        {
            Height = double.NaN;
            MinHeight = PasswordRowMinHeight;
            MaxHeight = double.PositiveInfinity;
        }

        /// <summary>
        /// Zeigt die Passwort-Eingabe (z. B. nach dem PasswordRequired-Ereignis des TeiPenServiceWrapper).
        /// </summary>
        public void ShowPasswordInputRequired()
        {
            StopConnectDelayTimer();
            _showPasswordInput = true;
            _isConnectMode = true;
            ApplyPasswordRowHeight();
            var connectLoadingButton = FindName("ConnectLoadingButton") as LoadingButton;
            var passwordPanel = FindName("PasswordInputPanel") as FrameworkElement;
            var hoverConnectLoadingButton = FindName("HoverConnectLoadingButton") as LoadingButton;
            if (connectLoadingButton != null)
            {
                connectLoadingButton.IsLoading = false;
                connectLoadingButton.Visibility = Visibility.Collapsed;
            }
            if (hoverConnectLoadingButton != null)
                hoverConnectLoadingButton.IsLoading = false;
            if (passwordPanel != null)
                passwordPanel.Visibility = Visibility.Visible;
            SetPasswordInputError(false);
            UpdateViewsVisibility();
            var passwordBox = FindName("PenPasswordBox") as PasswordBox;
            if (passwordBox != null)
                _ = Dispatcher.InvokeAsync(() => passwordBox.Focus(), DispatcherPriority.Loaded);
        }

        /// <summary>
        /// Blendet die Passwort-Eingabe nach erfolgreicher Übermittlung aus.
        /// </summary>
        public void HidePasswordInputAfterSubmit()
        {
            ClearPasswordUiIfVisible();
            UpdateViewsVisibility();
        }

        /// <summary>
        /// Hebt die Passwort-Zeile rot hervor (falsches oder leeres Passwort).
        /// </summary>
        public void SetPasswordInputError(bool isError)
        {
            _isPasswordError = isError;
            var passwordFieldBorder = FindName("PasswordFieldBorder") as Border;
            var errorBrush = TryFindBrush("StatusErrorBrush");
            if (passwordFieldBorder == null)
                return;
            if (isError && errorBrush != null)
                passwordFieldBorder.BorderBrush = errorBrush;
            else
                passwordFieldBorder.BorderBrush = Brushes.Black;
        }

        private void StopConnectDelayTimer()
        {
            if (_connectDelayTimer != null)
            {
                _connectDelayTimer.Tick -= ConnectDelayTimer_Tick;
                _connectDelayTimer.Stop();
                _connectDelayTimer = null;
            }
        }

        private void ConnectDelayTimer_Tick(object sender, EventArgs e)
        {
            StopConnectDelayTimer();
            ShowPasswordInputRequired();
        }

        private void PasswordBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (_isPasswordError) return;
            var border = FindName("PasswordFieldBorder") as Border;
            if (border == null) return;
            var pastelGreen = TryFindBrush("TeiGreenPastelBrush");
            if (pastelGreen != null) border.BorderBrush = pastelGreen;
        }

        private void PasswordBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_isPasswordError) return;
            var border = FindName("PasswordFieldBorder") as Border;
            if (border == null) return;
            border.BorderBrush = Brushes.Black;
        }

        private void PasswordSubmitButton_Click(object sender, RoutedEventArgs e)
        {
            var passwordBox = FindName("PenPasswordBox") as PasswordBox;
            string pwd = passwordBox?.Password ?? string.Empty;
            if (string.IsNullOrEmpty(pwd))
            {
                SetPasswordInputError(true);
                return;
            }

            SetPasswordInputError(false);
            RaisePasswordSubmitted(pwd);
        }

        private void PasswordSubmitButton_MouseEnter(object sender, MouseEventArgs e) => ApplyPasswordSubmitButtonAppearance(false, true);
        private void PasswordSubmitButton_MouseLeave(object sender, MouseEventArgs e) => ApplyPasswordSubmitButtonAppearance(false, false);
        private void PasswordSubmitButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => ApplyPasswordSubmitButtonAppearance(true, true);
        private void PasswordSubmitButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var border = FindName("PasswordSubmitButton") as Border;
            ApplyPasswordSubmitButtonAppearance(false, border != null && border.IsMouseOver);
        }

        private void ApplyPasswordSubmitButtonAppearance(bool isPressed, bool isHover)
        {
            var border = FindName("PasswordSubmitButton") as Border;
            var textBlock = FindName("PasswordSubmitButtonText") as TextBlock;
            var pastelGreen = TryFindBrush("TeiGreenPastelBrush");
            if (border == null || textBlock == null) return;
            if (isPressed) { border.BorderBrush = Brushes.Black; textBlock.Foreground = Brushes.Black; }
            else if (isHover && pastelGreen != null) { border.BorderBrush = pastelGreen; textBlock.Foreground = pastelGreen; }
            else { border.BorderBrush = Brushes.Black; textBlock.Foreground = Brushes.Black; }
        }

        // --- Paired: Remove / Connect / Disconnect (Hover) ---
        private void HoverRemoveButton_Click(object sender, RoutedEventArgs e) => RaiseRemoveRequested();

        private void HoverConnectButton_Click(object sender, RoutedEventArgs e)
        {
            var hoverConnectLoadingButton = FindName("HoverConnectLoadingButton") as LoadingButton;
            if (hoverConnectLoadingButton != null) hoverConnectLoadingButton.IsLoading = true;
            RaiseConnectRequested();
        }

        private void HoverDisconnectButton_Click(object sender, RoutedEventArgs e) => RaiseDisconnectRequested();

        // --- Connected: Disconnect button appearance ---
        private void DisconnectButton_MouseEnter(object sender, MouseEventArgs e)
        {
            var border = sender as Border;
            ApplyDisconnectButtonAppearance(border, isPressed: false, isHover: true);
            if (border != null)
            {
                var hoverBg = TryFindBrush("TitleBarHoverBrush");
                if (hoverBg != null) border.Background = hoverBg;
            }
        }

        private void DisconnectButton_MouseLeave(object sender, MouseEventArgs e)
        {
            var border = sender as Border;
            ApplyDisconnectButtonAppearance(border, isPressed: false, isHover: false);
            if (border != null) border.Background = Brushes.Transparent;
        }

        private void DisconnectButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => ApplyDisconnectButtonAppearance(sender as Border, isPressed: true, isHover: true);

        private void DisconnectButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            ApplyDisconnectButtonAppearance(border, isPressed: false, isHover: border != null && border.IsMouseOver);
        }

        private void ApplyDisconnectButtonAppearance(Border border, bool isPressed, bool isHover)
        {
            if (border == null) return;
            var textBlock = border.Child as TextBlock;
            if (textBlock == null) return;
            var errorBrush = TryFindBrush("StatusErrorBrush");
            if (isPressed) { border.BorderBrush = Brushes.Black; textBlock.Foreground = Brushes.Black; }
            else if (isHover && errorBrush != null) { border.BorderBrush = errorBrush; textBlock.Foreground = errorBrush; }
            else
            {
                border.BorderBrush = new SolidColorBrush(Color.FromArgb(0x4D, 0, 0, 0));
                var textBrush = TryFindBrush("TextBrush");
                textBlock.Foreground = textBrush ?? Brushes.Black;
            }
        }
    }
}

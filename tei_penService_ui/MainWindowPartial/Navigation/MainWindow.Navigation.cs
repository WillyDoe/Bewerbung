using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using tei_penService_ui.Helpers;
using tei_penService_ui.Interfaces;
using tei_penService_ui.Models;
using tei_penService_ui.Views;

namespace tei_penService_ui
{
    /// <summary>
    /// View-Navigation (Start, Login, Workspace, Settings), Login-Overlay und Status-Indikatoren für MainWindow.
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>Behandelt Klick auf „Starten"/„ohne Stift starten". Zeigt Workspace, Login oder verknüpften Benutzer.</summary>
        private async void StartWithoutPenButton_Click(object sender, RoutedEventArgs e)
        {
            var control = sender as FrameworkElement;
            if (control == null)
                return;

            try
            {
                control.IsEnabled = false;

                if (_currentUser != null)
                {
                    ShowWorkspaceView();
                }
                else
                {
                    var connectedInfo = _teiPenServiceWrapper?.GetConnectedPenInfo();
                    UserMemoryEntry linkedUser = null;
                    if (connectedInfo != null && _appDataService != null)
                    {
                        string mac = MacAddressHelper.NormalizeMacAddress(new ConnectedPenDisplayInfo(connectedInfo));
                        if (!string.IsNullOrEmpty(mac))
                            linkedUser = await _appDataService.GetUserByLinkedPenAsync(mac).ConfigureAwait(true);
                    }

                    if (linkedUser != null)
                    {
                        _currentUser = linkedUser;
                        UpdateLoggedInStatusIndicator();
                        ShowWorkspaceView();
                    }
                    else
                    {
                        ShowLoginView();
                    }
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler beim Starten: {ex.Message}");
            }
            finally
            {
                control.IsEnabled = true;
            }
        }

        /// <summary>
        /// Zeigt die Login/Registrierungs-Ansicht als kleines Overlay-Fenster vor der StartView.
        /// </summary>
        private void ShowLoginView()
        {
            if (_loginView == null)
                return;
            _loginView.ClearForm();
            var host = GetMainContentHost();
            if (host != null)
                host.Content = _startView;
            _currentView = _startView;
            var overlayContent = FindName("LoginOverlayContent") as ContentControl;
            var overlay = FindName("LoginOverlay") as Border;
            if (overlayContent != null && overlay != null)
            {
                overlayContent.Content = _loginView;
                overlay.Visibility = Visibility.Visible;
            }
            UpdateHomeButtonVisibility();
        }

        /// <summary>
        /// Blendet das Login-Overlay aus.
        /// </summary>
        private void HideLoginOverlay()
        {
            var overlay = FindName("LoginOverlay") as Border;
            if (overlay != null)
                overlay.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Event-Handler, wenn die Login-Maske geschlossen werden soll (z.B. per X-Button).
        /// </summary>
        private void OnLoginCloseRequested(object sender, EventArgs e)
        {
            HideLoginOverlay();
        }

        /// <summary>
        /// Event-Handler für erfolgreiche Anmeldung oder Registrierung.
        /// </summary>
        private void OnLoginSucceeded(object sender, UserMemoryEntry user)
        {
            _currentUser = user;
            HideLoginOverlay();
            UpdateLoggedInStatusIndicator();
            ShowWorkspaceView();
        }

        /// <summary>
        /// Zeigt den angegebenen View im Content-Bereich an.
        /// </summary>
        private void ShowView(UserControl view)
        {
            if (view == null)
                return;
            _currentView = view;
            var host = GetMainContentHost();
            if (host != null)
                host.Content = view;
            UpdateHomeButtonVisibility();
        }

        /// <summary>
        /// Zeigt den Home-Button an, wenn WorkspaceView oder LoginView angezeigt wird.
        /// </summary>
        private void UpdateHomeButtonVisibility()
        {
            var visible = _currentView is WorkspaceView || _currentView is LoginView || _currentView is SettingsView;
            if (FindName("HomeButton") is Button homeButton)
                homeButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            if (FindName("SettingsButton") is Button settingsButton)
                settingsButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Wechselt in den Arbeitsbereich-View (Platzhalter für Datenübertragung/Rendering).
        /// </summary>
        private void ShowWorkspaceView()
        {
            ITeiPenServiceWrapper wrapper = _teiPenServiceWrapper;
            if (_workspaceView == null)
            {
                _workspaceView = wrapper != null
                    ? new WorkspaceView(wrapper)
                    : new WorkspaceView();
            }
            else if (wrapper != null && !_workspaceView.UsesPenServiceWrapper)
            {
                // Vorher z. B. vor MainWindow_Loaded ohne Wrapper erzeugt — mit Stift neu aufsetzen.
                _workspaceView = new WorkspaceView(wrapper);
            }

            ShowView(_workspaceView);
        }

        /// <summary>
        /// Wechselt in die Einstellungs-Ansicht.
        /// </summary>
        private async void ShowSettingsView()
        {
            if (_settingsView == null)
            {
                _settingsView = new SettingsView();
                _settingsView.CloseRequested += (s, e) =>
                {
                    ShowWorkspaceView();
                    _ = UpdatePairedPensAsync();
                };
                _settingsView.CurrentUserUpdated += (s, updatedUser) =>
                {
                    if (updatedUser != null)
                        _currentUser = updatedUser;
                };
            }
            if (_appDataService != null && _currentUser != null)
                await _settingsView.SetContextAsync(_appDataService, _currentUser, _teiPenServiceWrapper).ConfigureAwait(true);
            ShowView(_settingsView);
        }

        /// <summary>
        /// Behandelt Klick auf den Home-Button. Wechselt zurück zur Start-Ansicht.
        /// </summary>
        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            HideLoginOverlay();
            UpdateLoggedInStatusIndicator();
            ShowView(_startView);
            UpdateStartViewLoggedInState();
        }

        /// <summary>
        /// Behandelt Klick auf den Einstellungen-Button. Wechselt zur Einstellungs-Ansicht.
        /// </summary>
        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowSettingsView();
        }

        /// <summary>
        /// Aktualisiert die Farbe des Stift-verbunden-Status-Indikators (TeiGreen bei verbunden, Grau bei nicht verbunden).
        /// </summary>
        private void UpdatePenConnectedStatusIndicator()
        {
            var indicator = FindName("PenConnectedStatusIndicator") as System.Windows.Shapes.Ellipse;
            if (indicator == null)
                return;

            bool isConnected = false;
            try
            {
                isConnected = _teiPenServiceWrapper?.GetConnectedPenInfo() != null;
            }
            catch { }

            indicator.Fill = GetStatusIndicatorBrush(isConnected);
        }

        /// <summary>
        /// Aktualisiert die Farbe des Angemeldet-Status-Indikators (TeiGreen bei angemeldet, Grau bei nicht angemeldet).
        /// </summary>
        private void UpdateLoggedInStatusIndicator()
        {
            var indicator = FindName("LoggedInStatusIndicator") as System.Windows.Shapes.Ellipse;
            if (indicator == null)
                return;

            bool isLoggedIn = _currentUser != null;
            indicator.Fill = GetStatusIndicatorBrush(isLoggedIn);
        }

        /// <summary>
        /// Aktualisiert den Anmeldestatus in der StartView (Sichtbarkeit des Abmelden-Buttons).
        /// </summary>
        private void UpdateStartViewLoggedInState()
        {
            GetStartView()?.SetLoggedIn(_currentUser != null);
        }

        /// <summary>
        /// Behandelt Abmelden aus der StartView. Setzt den aktuellen Benutzer zurück und aktualisiert Statusleiste sowie Abmelden-Button.
        /// </summary>
        private void OnStartViewLogoutRequested(object sender, EventArgs e)
        {
            _currentUser = null;
            UpdateLoggedInStatusIndicator();
            GetStartView()?.SetLoggedIn(false);
        }
    }
}

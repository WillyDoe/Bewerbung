using System;
using System.Windows;
using System.Windows.Controls;
using tei_penService_ui.Models;
using tei_penService_ui.Services;

namespace tei_penService_ui.Views
{
    /// <summary>
    /// Interaktionslogik für LoginView.xaml
    /// </summary>
    public partial class LoginView : UserControl
    {
        /// <summary>
        /// Wird ausgelöst, wenn die Anmeldung oder Registrierung erfolgreich war.
        /// </summary>
        public event EventHandler<UserMemoryEntry> LoginSucceeded;

        /// <summary>
        /// Wird ausgelöst, wenn die Maske geschlossen werden soll (z.B. per X-Button).
        /// </summary>
        public event EventHandler CloseRequested;

        private AppDataService _appDataService; // Kann null sein

        public LoginView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Setzt den AppDataService für Login/Registrierung.
        /// </summary>
        public void SetAppDataService(AppDataService service)
        {
            _appDataService = service; // Darf null sein
        }

        /// <summary>
        /// Setzt die gesamte Eingabemaske zurück (alle Felder und Fehler). Wird beim erneuten Öffnen der Maske aufgerufen.
        /// </summary>
        public void ClearForm()
        {
            LoginEmailInput.Text = string.Empty;
            LoginPasswordInput.Password = string.Empty;
            RegisterDisplayNameInput.Text = string.Empty;
            RegisterEmailInput.Text = string.Empty;
            RegisterPasswordInput.Password = string.Empty;
            ClearErrors();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void TabLogin_Click(object sender, RoutedEventArgs e)
        {
            TabLogin.IsChecked = true;
            TabRegister.IsChecked = false;
            LoginPanel.Visibility = Visibility.Visible;
            RegisterPanel.Visibility = Visibility.Collapsed;
            LoginButton.IsDefault = true;
            RegisterButton.IsDefault = false;
            ClearErrors();
        }

        private void TabRegister_Click(object sender, RoutedEventArgs e)
        {
            TabLogin.IsChecked = false;
            TabRegister.IsChecked = true;
            LoginPanel.Visibility = Visibility.Collapsed;
            RegisterPanel.Visibility = Visibility.Visible;
            LoginButton.IsDefault = false;
            RegisterButton.IsDefault = true;
            ClearErrors();
        }

        private void ClearErrors()
        {
            LoginError.Text = string.Empty;
            LoginError.Visibility = Visibility.Collapsed;
            RegisterError.Text = string.Empty;
            RegisterError.Visibility = Visibility.Collapsed;
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            if (_appDataService == null)
            {
                ShowLoginError("Service nicht verfügbar.");
                return;
            }

            string loginInput = LoginEmailInput.Text?.Trim() ?? string.Empty;
            string password = LoginPasswordInput.Password ?? string.Empty;

            if (string.IsNullOrEmpty(loginInput))
            {
                ShowLoginError("Bitte E-Mail oder Anmeldename eingeben.");
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                ShowLoginError("Bitte Passwort eingeben.");
                return;
            }

            try
            {
                LoginButton.IsEnabled = false;
                ClearErrors();

                var user = await _appDataService.ValidateLoginAsync(loginInput, password);

                if (user != null)
                {
                    ClearForm();
                    LoginSucceeded?.Invoke(this, user);
                }
                else
                {
                    ClearLoginPassword();
                    ShowLoginError("Falsche Anmeldedaten.");
                }
            }
            catch (Exception ex)
            {
                ClearLoginPassword();
                ShowLoginError($"Fehler: {ex.Message}");
            }
            finally
            {
                LoginButton.IsEnabled = true;
            }
        }

        private async void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            if (_appDataService == null)
            {
                ShowRegisterError("Service nicht verfügbar.");
                return;
            }

            string displayName = RegisterDisplayNameInput.Text?.Trim() ?? string.Empty;
            string email = RegisterEmailInput.Text?.Trim() ?? string.Empty;
            string password = RegisterPasswordInput.Password ?? string.Empty;

            if (string.IsNullOrEmpty(displayName))
            {
                ShowRegisterError("Bitte Anzeigename eingeben.");
                return;
            }

            if (string.IsNullOrEmpty(email))
            {
                ShowRegisterError("Bitte E-Mail eingeben.");
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                ShowRegisterError("Bitte Passwort eingeben.");
                return;
            }

            try
            {
                RegisterButton.IsEnabled = false;
                ClearErrors();

                var user = await _appDataService.CreateUserAsync(email, displayName, password);

                if (user != null)
                {
                    ClearForm();
                    LoginSucceeded?.Invoke(this, user);
                }
                else
                {
                    ClearRegisterPassword();
                    ShowRegisterError("E-Mail bereits registriert.");
                }
            }
            catch (Exception ex)
            {
                ClearRegisterPassword();
                ShowRegisterError($"Fehler: {ex.Message}");
            }
            finally
            {
                RegisterButton.IsEnabled = true;
            }
        }

        private void ClearLoginPassword()
        {
            LoginPasswordInput.Password = string.Empty;
        }

        private void ClearRegisterPassword()
        {
            RegisterPasswordInput.Password = string.Empty;
        }

        private void ShowLoginError(string message)
        {
            LoginError.Text = message;
            LoginError.Visibility = Visibility.Visible;
        }

        private void ShowRegisterError(string message)
        {
            RegisterError.Text = message;
            RegisterError.Visibility = Visibility.Visible;
        }
    }
}

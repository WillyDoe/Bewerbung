using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TeiPenServiceConnectionManager.Models;
using tei_penService_ui.Controls;
using tei_penService_ui.Models;
using tei_penService_ui.Services;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace tei_penService_ui.Views
{
    /// <summary>
    /// Einstellungs-Ansicht. Schließen-Button (×) oben rechts wechselt zurück zum Arbeitsbereich.
    /// Nutzernamen (Stift/Anwendung), E-Mail/Passwort ändern, Stift-Account-Verknüpfung; Persistenz in app_data.json.
    /// </summary>
    public partial class SettingsView : UserControl
    {
        /// <summary>
        /// Wird ausgelöst, wenn der Benutzer die Einstellungen schließen und zum Arbeitsbereich zurückkehren möchte.
        /// </summary>
        public event EventHandler CloseRequested;

        /// <summary>
        /// Wird ausgelöst, wenn sich Benutzerdaten (z. B. E-Mail, Anzeigename) geändert haben, damit das MainWindow _currentUser aktualisieren kann.
        /// </summary>
        public event EventHandler<UserMemoryEntry> CurrentUserUpdated;

        /// <summary>Service für Lese-/Schreibzugriff auf app_data.json (Benutzer, Stiftdaten, Verknüpfungen).</summary>
        private AppDataService _appDataService;
        /// <summary>Der aktuell in den Einstellungen bearbeitete Benutzer (Anzeigename, E-Mail, Passwort, verknüpfter Stift).</summary>
        private UserMemoryEntry _currentUser;
        /// <summary>Für die Stift-Auswahllisten aufbereitete Einträge (MAC-Adresse + Anzeigename).</summary>
        private List<PenListItem> _penListItems;
        /// <summary>Alle gepaarten Stifte, indiziert nach normalisierter MAC-Adresse, für schnellen Zugriff auf PenMemoryEntry.</summary>
        private Dictionary<string, PenMemoryEntry> _pairedPensByMac;
        /// <summary>Optionaler Wrapper für Bluetooth-Stiftzugriff (Anzeigename/Passwort am Gerät setzen).</summary>
        private TeiPenServiceWrapper _penServiceWrapper;

        /// <summary>Breite des linken Menüpanels in Pixeln, wenn es ausgeklappt ist.</summary>
        private const double MenuWidthExpanded = 220;
        /// <summary>Breite des linken Menüpanels in Pixeln, wenn es eingeklappt ist.</summary>
        private const double MenuWidthCollapsed = 48;
        /// <summary>Gibt an, ob das linke Einstellungsmenü aktuell ausgeklappt (true) oder eingeklappt (false) ist.</summary>
        private bool _menuExpanded = true;

        /// <summary>Timer für das ausblenden der vertikalen Scrollbar nachdem der Benutzer gestoppt hat zu scrollen.</summary>
        private DispatcherTimer _scrollBarTimer = new DispatcherTimer();

        /// <summary>ComboBox zur Auswahl des Stifts im Bereich „Stiftname ändern“.</summary>
        private SettingsComboBoxControl PenListComboBox => (SettingsComboBoxControl)PenNameBlockContainer.Inputs[0];
        /// <summary>Textfeld für den Anzeigenamen des ausgewählten Stifts.</summary>
        private FormularTextInputControl PenNameTextBox => (FormularTextInputControl)PenNameBlockContainer.Inputs[1];
        /// <summary>Button zum Speichern des Stift-Anzeigenamens.</summary>
        private Button SavePenNameButton => (Button)PenNameBlockContainer.Buttons[0];
        /// <summary>ComboBox zur Auswahl des Stifts im Bereich „Stift-Passwort ändern“.</summary>
        private SettingsComboBoxControl PenPasswordPenComboBox => (SettingsComboBoxControl)PenPasswordBlockContainer.Inputs[0];
        /// <summary>Hinweistext zum aktuell mit dem Konto verknüpften Stift.</summary>
        private TextBlock LinkedPenInfoTextBlock => (TextBlock)LinkPenBlockContainer.Inputs[0];
        /// <summary>ComboBox zur Auswahl des Stifts für die Konto-Verknüpfung.</summary>
        private SettingsComboBoxControl LinkPenComboBox => (SettingsComboBoxControl)LinkPenBlockContainer.Inputs[1];
        /// <summary>Eingabefeld für das aktuelle Stift-Passwort (zum Ändern).</summary>
        private FormularPasswordInputControl PenCurrentPasswordInput => PenPasswordBlockContainer.Inputs.Count > 1 ? (FormularPasswordInputControl)PenPasswordBlockContainer.Inputs[1] : null;
        /// <summary>Eingabefeld für das neue Stift-Passwort.</summary>
        private FormularPasswordInputControl PenNewPasswordInput => PenPasswordBlockContainer.Inputs.Count > 2 ? (FormularPasswordInputControl)PenPasswordBlockContainer.Inputs[2] : null;
        /// <summary>Eingabefeld zur Wiederholung des neuen Stift-Passworts.</summary>
        private FormularPasswordInputControl PenNewPasswordRepeatInput => PenPasswordBlockContainer.Inputs.Count > 3 ? (FormularPasswordInputControl)PenPasswordBlockContainer.Inputs[3] : null;
        /// <summary>Button zum Speichern des geänderten Stift-Passworts.</summary>
        private Button SavePenPasswordButton => (Button)PenPasswordBlockContainer.Buttons[0];
        /// <summary>Button zum Verknüpfen des ausgewählten Stifts mit dem Benutzerkonto.</summary>
        private Button LinkPenButton => LinkPenBlockContainer.Buttons.Count > 0 ? (Button)LinkPenBlockContainer.Buttons[0] : null;
        /// <summary>Button zum Aufheben der Verknüpfung zwischen Konto und Stift.</summary>
        private Button UnlinkPenButton => LinkPenBlockContainer.Buttons.Count > 1 ? (Button)LinkPenBlockContainer.Buttons[1] : null;

        /// <summary>
        /// Initialisiert die Einstellungsansicht, legt interne Sammlungen an und registriert Ereignishandler.
        /// </summary>
        public SettingsView()
        {
            InitializeComponent();
            _penListItems = new List<PenListItem>();
            _pairedPensByMac = new Dictionary<string, PenMemoryEntry>(StringComparer.OrdinalIgnoreCase);
            Loaded += SettingsView_Loaded;
            Unloaded += SettingsView_Unloaded;
            _scrollBarTimer.Interval = TimeSpan.FromMilliseconds(750);
            _scrollBarTimer.Tick += OnScrollBarTimerTick;
            ContentScrollViewer.ScrollChanged += OnContentScrollViewerScroll;

        }

        /// <summary>
        /// Wird nach dem Laden der View aufgerufen und setzt den Standard-Menüeintrag.
        /// </summary>
        private void SettingsView_Loaded(object sender, RoutedEventArgs e)
        {
            SettingsMenuListBox.SelectedIndex = 0;
        }

        /// <summary>
        /// Wird beim Entfernen der View aus dem visuellen Baum aufgerufen; meldet Event-Handler ab und stoppt ggf. laufende Timer.
        /// </summary>
        private void SettingsView_Unloaded(object sender, RoutedEventArgs e)
        {
            ContentScrollViewer.ScrollChanged -= OnContentScrollViewerScroll;
            _scrollBarTimer.Stop();
            _scrollBarTimer.Tick -= OnScrollBarTimerTick;
            
        }

        /// <summary>
        /// Klappt das linke Einstellungsmenü ein oder aus und passt Breite und Sichtbarkeit der Beschriftungen an.
        /// </summary>
        private void MenuExpandCollapseButton_Click(object sender, RoutedEventArgs e)
        {
            _menuExpanded = !_menuExpanded;
            SettingsMenuPanel.Width = _menuExpanded ? MenuWidthExpanded : MenuWidthCollapsed;
            MenuItemsPanel.Visibility = _menuExpanded ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Reagiert auf einen Wechsel des ausgewählten Menüeintrags und blendet den passenden Inhaltsbereich ein.
        /// </summary>
        private void SettingsMenuListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SettingsMenuListBox.SelectedItem == MenuItemAccount)
            {
                AccountContentPanel.Visibility = Visibility.Visible;
                TeiPenContentPanel.Visibility = Visibility.Collapsed;
            }
            else if (SettingsMenuListBox.SelectedItem == MenuItemTeiPen)
            {
                AccountContentPanel.Visibility = Visibility.Collapsed;
                TeiPenContentPanel.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// Setzt den Kontext (AppDataService und aktueller User). Sollte vor dem Anzeigen der View aufgerufen werden.
        /// </summary>
        /// <param name="appDataService">Service für Persistenz; darf nicht null sein.</param>
        /// <param name="currentUser">Der in den Einstellungen bearbeitete Benutzer; darf nicht null sein.</param>
        /// <param name="penServiceWrapper">Optional; wenn gesetzt, kann Anzeigename/Passwort am verbundenen Stift gesetzt werden.</param>
        public async System.Threading.Tasks.Task SetContextAsync(AppDataService appDataService, UserMemoryEntry currentUser, TeiPenServiceWrapper penServiceWrapper = null)
        {
            _appDataService = appDataService ?? throw new ArgumentNullException(nameof(appDataService));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _penServiceWrapper = penServiceWrapper;
            await LoadPairedPensAsync().ConfigureAwait(true);
            BindToCurrentUser();
        }

        /// <summary>
        /// Lädt alle in der App bekannten, mit Nutzern verknüpften Stifte und bereitet sie zur Anzeige in den Auswahllisten auf.
        /// </summary>
        private async Task LoadPairedPensAsync()
        {
            if (_appDataService == null)
                return;
            try
            {
                var pens = await _appDataService.GetPairedPensAsync().ConfigureAwait(true);
                _pairedPensByMac = new Dictionary<string, PenMemoryEntry>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in pens)
                    _pairedPensByMac[kv.Key] = kv.Value;
                _penListItems = pens.Select(kv =>
                {
                    var name = !string.IsNullOrEmpty(kv.Value.PenName) ? kv.Value.PenName : (kv.Value.DisplayName ?? kv.Key);
                    return new PenListItem(kv.Key, name);
                }).ToList();
            }
            catch (Exception ex)
            {
                ShowStatus($"Fehler beim Laden der Stiftliste: {ex.Message}", isError: true);
            }
        }

        /// <summary>
        /// Überträgt Benutzerdaten und Stiftlisten auf die Oberfläche und aktualisiert die Verknüpfungsanzeige.
        /// </summary>
        private void BindToCurrentUser()
        {
            if (_currentUser == null)
                return;
            ((FormularTextInputControl)DisplayNameBlockContainer.Inputs[0]).Text = _currentUser.DisplayName ?? string.Empty;
            ((FormularTextInputControl)EmailBlockContainer.Inputs[0]).Text = _currentUser.Email ?? string.Empty;

            PenListComboBox.ItemsSource = null;
            PenListComboBox.ItemsSource = _penListItems;
            PenPasswordPenComboBox.ItemsSource = null;
            PenPasswordPenComboBox.ItemsSource = _penListItems.ToList();
            LinkPenComboBox.ItemsSource = null;
            LinkPenComboBox.ItemsSource = _penListItems.ToList();

            UpdateLinkedPenInfo();
            if (_penListItems.Count > 0 && !string.IsNullOrEmpty(_currentUser.LinkedPenMacAddress))
            {
                var linked = _penListItems.FirstOrDefault(p => string.Equals(p.MacAddress, _currentUser.LinkedPenMacAddress?.Trim(), StringComparison.OrdinalIgnoreCase));
                if (linked != null)
                    LinkPenComboBox.SelectedItem = linked;
            }
            if (UnlinkPenButton != null)
                UnlinkPenButton.IsEnabled = !string.IsNullOrWhiteSpace(_currentUser.LinkedPenMacAddress);
            if (LinkPenButton != null)
                LinkPenButton.IsEnabled = LinkPenComboBox.SelectedItem != null;

            ApplyPenNameEditState(PenListComboBox.SelectedItem as PenListItem);
            ApplyPenPasswordEditState(PenPasswordPenComboBox.SelectedItem != null);
        }

        private void ApplyPenNameEditState(PenListItem selectedItem, PenMemoryEntry entry = null)
        {
            if (selectedItem != null && entry == null)
                _pairedPensByMac.TryGetValue(selectedItem.MacAddress, out entry);

            if (selectedItem != null && entry != null)
            {
                PenNameTextBox.Visibility = Visibility.Visible;
                PenNameTextBox.Text = !string.IsNullOrEmpty(entry.PenName) ? entry.PenName : (entry.DisplayName ?? selectedItem.DisplayLabel);
                PenNameTextBox.IsEnabled = true;
                SavePenNameButton.IsEnabled = true;
                PenListComboBox.Label = string.IsNullOrEmpty(entry.DisplayName) ? "Stift" : "Stift (Modell: " + entry.DisplayName + ")";
                return;
            }

            PenNameTextBox.Visibility = Visibility.Collapsed;
            PenNameTextBox.Text = string.Empty;
            PenNameTextBox.IsEnabled = false;
            SavePenNameButton.IsEnabled = false;
            PenListComboBox.Label = "Stift";
        }

        private void ApplyPenPasswordEditState(bool hasSelection)
        {
            SetPenPasswordInputsVisibility(hasSelection ? Visibility.Visible : Visibility.Collapsed);
            SavePenPasswordButton.IsEnabled = hasSelection;
        }

        /// <summary>
        /// Aktualisiert den Hinweistext zum aktuell mit dem Benutzerkonto verknüpften Stift.
        /// </summary>
        private void UpdateLinkedPenInfo()
        {
            if (string.IsNullOrWhiteSpace(_currentUser?.LinkedPenMacAddress))
            {
                LinkedPenInfoTextBlock.Text = "Kein Stift verknüpft. Bei verknüpftem Stift wird beim Klick auf „Starten“ keine Anmeldemaske angezeigt.";
                if (UnlinkPenButton != null)
                    UnlinkPenButton.IsEnabled = false;
                return;
            }
            string mac = _currentUser.LinkedPenMacAddress.Trim();
            string macCanonical = AppDataService.NormalizeMacAddressCanonical(mac);
            if (!string.IsNullOrEmpty(macCanonical) && _pairedPensByMac.TryGetValue(macCanonical, out var entry))
            {
                var name = !string.IsNullOrEmpty(entry.PenName) ? entry.PenName : (entry.DisplayName ?? mac);
                LinkedPenInfoTextBlock.Text = $"Verknüpfter Stift: {name} ({mac})";
            }
            else
                LinkedPenInfoTextBlock.Text = $"Verknüpfter Stift: {mac}";
            if (UnlinkPenButton != null)
                UnlinkPenButton.IsEnabled = true;
        }

        /// <summary>
        /// Lädt beim Wechsel des ausgewählten Stifts dessen aktuellen Anzeigenamen und Modellbezeichnung in die Eingabefelder.
        /// </summary>
        private void PenListComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PenListComboBox.SelectedItem is PenListItem item && _pairedPensByMac.TryGetValue(item.MacAddress, out var entry))
            {
                // Anzeigename = editierbarer App-Name, DisplayName = Modell vom Gerät.
                ApplyPenNameEditState(item, entry);
            }
            else
            {
                ApplyPenNameEditState(null);
            }
        }

        /// <summary>
        /// Speichert den vom Benutzer eingegebenen Anzeigenamen für den ausgewählten Stift in den App-Daten und optional am Gerät.
        /// </summary>
        private async void SavePenNameButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(PenListComboBox.SelectedItem is PenListItem item) || _appDataService == null)
            {
                if (_appDataService != null)
                    ShowStatus("Bitte zuerst einen Stift auswählen.", isError: true);
                return;
            }
            string name = PenNameTextBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(name))
            {
                ShowStatus("Bitte einen Namen eingeben.", isError: true);
                return;
            }
            try
            {
                bool ok = await _appDataService.UpdatePenNameAsync(item.MacAddress, name).ConfigureAwait(true);
                if (ok)
                {
                    bool deviceUpdated = false;
                    if (_penServiceWrapper != null && _penServiceWrapper.IsPenConnected(item.MacAddress))
                    {
                        string nameForDevice = name.Length > 16 ? name.Substring(0, 16) : name;
                        try
                        {
                            deviceUpdated = await _penServiceWrapper.SetDisplayNameAsync(nameForDevice).ConfigureAwait(true);
                        }
                        catch (Exception exDev)
                        {
                            ShowStatus($"Anzeigename in der App gespeichert. Am Gerät: {exDev.Message}", isError: true);
                        }
                    }
                    if (_pairedPensByMac.TryGetValue(item.MacAddress, out var entry))
                        entry.PenName = name;
                    item.DisplayLabel = name;
                    PenListComboBox.ItemsSource = null;
                    PenListComboBox.ItemsSource = _penListItems;
                    if (deviceUpdated)
                        ShowStatus(name.Length > 16 ? "Anzeigename gespeichert. In der App vollständig; am Gerät (Bluetooth) max. 16 Zeichen." : "Anzeigename in App und am Gerät (Bluetooth) gespeichert.");
                    else if (_penServiceWrapper != null && _penServiceWrapper.IsPenConnected(item.MacAddress))
                        ShowStatus("Anzeigename in der App gespeichert. Am Gerät konnte der Name nicht gesetzt werden.", isError: true);
                    else
                        ShowStatus("Anzeigename des Stifts gespeichert.");
                }
                else
                    ShowStatus("Stift nicht gefunden oder Fehler beim Speichern.", isError: true);
            }
            catch (Exception ex)
            {
                ShowStatus($"Fehler: {ex.Message}", isError: true);
            }
        }

        /// <summary>
        /// Speichert den Anzeigenamen des aktuellen Benutzers in den App-Daten und informiert das Hauptfenster über die Änderung.
        /// </summary>
        private async void SaveAppDisplayNameButton_Click(object sender, RoutedEventArgs e)
        {
            if (_appDataService == null || _currentUser == null)
                return;
            string name = ((FormularTextInputControl)DisplayNameBlockContainer.Inputs[0]).Text?.Trim() ?? string.Empty;
            try
            {
                bool ok = await _appDataService.UpdateUserDisplayNameAsync(_currentUser.Id, name).ConfigureAwait(true);
                if (ok)
                {
                    _currentUser.DisplayName = name;
                    CurrentUserUpdated?.Invoke(this, _currentUser);
                    ShowStatus("Anzeigename gespeichert.");
                }
                else
                    ShowStatus("Fehler beim Speichern.", isError: true);
            }
            catch (Exception ex)
            {
                ShowStatus($"Fehler: {ex.Message}", isError: true);
            }
        }

        /// <summary>
        /// Aktualisiert die im Benutzerkonto hinterlegte E-Mail-Adresse und lädt den Benutzer anschließend neu.
        /// </summary>
        private async void SaveEmailButton_Click(object sender, RoutedEventArgs e)
        {
            if (_appDataService == null || _currentUser == null)
                return;
            string newEmail = ((FormularTextInputControl)EmailBlockContainer.Inputs[0]).Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(newEmail))
            {
                ShowStatus("Bitte eine E-Mail-Adresse eingeben.", isError: true);
                return;
            }
            try
            {
                bool ok = await _appDataService.UpdateUserEmailAsync(_currentUser.Id, newEmail).ConfigureAwait(true);
                if (ok)
                {
                    _currentUser.Email = newEmail;
                    var updated = await _appDataService.GetUserByIdAsync(_currentUser.Id).ConfigureAwait(true);
                    if (updated != null)
                        CurrentUserUpdated?.Invoke(this, updated);
                    ShowStatus("E-Mail aktualisiert.");
                }
                else
                    ShowStatus("E-Mail bereits vergeben oder Fehler.", isError: true);
            }
            catch (Exception ex)
            {
                ShowStatus($"Fehler: {ex.Message}", isError: true);
            }
        }

        /// <summary>
        /// Setzt die Sichtbarkeit der drei Stift-Passwortfelder (aktuelles Passwort, neues Passwort, Wiederholung) im PenPasswordBlockContainer.
        /// </summary>
        /// <param name="visibility">Sichtbarkeit (Visible oder Collapsed).</param>
        private void SetPenPasswordInputsVisibility(Visibility visibility)
        {
            var inputs = PenPasswordBlockContainer?.Inputs;
            if (inputs == null) return;
            for (var i = 1; i <= 3 && i < inputs.Count; i++)
            {
                if (inputs[i] is UIElement el)
                    el.Visibility = visibility;
            }
        }

        /// <summary>
        /// Blendet beim Auswählen eines Stifts im Passwort-Bereich die Eingabefelder ein bzw. wieder aus.
        /// </summary>
        private void PenPasswordPenComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PenPasswordPenComboBox.SelectedItem != null)
            {
                ApplyPenPasswordEditState(true);
                if (PenNewPasswordInput != null) PenNewPasswordInput.Password = string.Empty;
                if (PenNewPasswordRepeatInput != null) PenNewPasswordRepeatInput.Password = string.Empty;
            }
            else
            {
                ApplyPenPasswordEditState(false);
            }
        }

        /// <summary>
        /// Speichert das neue Passwort für den ausgewählten Stift in den App-Daten und optional direkt am Gerät.
        /// </summary>
        private async void SavePenPasswordButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(PenPasswordPenComboBox.SelectedItem is PenListItem item) || _appDataService == null)
                return;
            string pwd = PenNewPasswordInput?.Password ?? string.Empty;
            string repeat = PenNewPasswordRepeatInput?.Password ?? string.Empty;
            if (pwd != repeat)
            {
                ShowStatus("Passwörter stimmen nicht überein.", isError: true);
                return;
            }
            try
            {
                bool ok = await _appDataService.SetPenPasswordInAppDataAsync(item.MacAddress, pwd).ConfigureAwait(true);
                if (ok)
                {
                    bool deviceUpdated = false;
                    if (_penServiceWrapper != null && _penServiceWrapper.IsPenConnected(item.MacAddress))
                    {
                        string oldPwd = PenCurrentPasswordInput?.Password ?? string.Empty;
                        try
                        {
                            deviceUpdated = await _penServiceWrapper.SetPasswordAsync(oldPwd, pwd).ConfigureAwait(true);
                        }
                        catch (Exception exDev)
                        {
                            ShowStatus($"App gespeichert. Gerät: {exDev.Message}", isError: true);
                        }
                    }
                    ShowStatus(deviceUpdated ? "Stift-Passwort in App und am Gerät gespeichert." : "Stift-Passwort in der App gespeichert.");
                    if (PenCurrentPasswordInput != null) PenCurrentPasswordInput.Password = string.Empty;
                    if (PenNewPasswordInput != null) PenNewPasswordInput.Password = string.Empty;
                    if (PenNewPasswordRepeatInput != null) PenNewPasswordRepeatInput.Password = string.Empty;
                }
                else
                    ShowStatus("Fehler beim Speichern.", isError: true);
            }
            catch (Exception ex)
            {
                ShowStatus($"Fehler: {ex.Message}", isError: true);
            }
        }

        /// <summary>
        /// Prüft das aktuelle Anwendungspasswort und speichert bei Erfolg ein neues Passwort für das Benutzerkonto.
        /// </summary>
        private async void SaveAppPasswordButton_Click(object sender, RoutedEventArgs e)
        {
            if (_appDataService == null || _currentUser == null)
                return;
            var currentInput = (FormularPasswordInputControl)PasswordBlockContainer.Inputs[0];
            var newInput = (FormularPasswordInputControl)PasswordBlockContainer.Inputs[1];
            var repeatInput = (FormularPasswordInputControl)PasswordBlockContainer.Inputs[2];
            string current = currentInput.Password ?? string.Empty;
            string newPwd = newInput.Password ?? string.Empty;
            string repeat = repeatInput.Password ?? string.Empty;
            if (_currentUser.Password != current)
            {
                ShowStatus("Aktuelles Passwort ist falsch.", isError: true);
                return;
            }
            if (newPwd != repeat)
            {
                ShowStatus("Neues Passwort und Wiederholung stimmen nicht überein.", isError: true);
                return;
            }
            if (string.IsNullOrEmpty(newPwd))
            {
                ShowStatus("Neues Passwort darf nicht leer sein.", isError: true);
                return;
            }
            try
            {
                bool ok = await _appDataService.UpdateUserPasswordAsync(_currentUser.Id, newPwd).ConfigureAwait(true);
                if (ok)
                {
                    _currentUser.Password = newPwd;
                    ShowStatus("Anwendungspasswort geändert.");
                    currentInput.Password = string.Empty;
                    newInput.Password = string.Empty;
                    repeatInput.Password = string.Empty;
                }
                else
                    ShowStatus("Fehler beim Speichern.", isError: true);
            }
            catch (Exception ex)
            {
                ShowStatus($"Fehler: {ex.Message}", isError: true);
            }
        }

        /// <summary>
        /// Aktiviert oder deaktiviert den Button „Stift mit diesem Konto verknüpfen“, je nachdem ob ein Stift in der ComboBox ausgewählt ist.
        /// </summary>
        private void LinkPenComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LinkPenButton != null)
                LinkPenButton.IsEnabled = LinkPenComboBox.SelectedItem != null;
        }

        /// <summary>
        /// Verknüpft den aktuell ausgewählten Stift mit dem Benutzerkonto und aktualisiert die Anzeige.
        /// </summary>
        private async void LinkPenButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(LinkPenComboBox.SelectedItem is PenListItem item) || _appDataService == null || _currentUser == null)
                return;
            try
            {
                bool ok = await _appDataService.LinkPenToUserAsync(item.MacAddress, _currentUser.Id).ConfigureAwait(true);
                if (ok)
                {
                    _currentUser.LinkedPenMacAddress = item.MacAddress;
                    UpdateLinkedPenInfo();
                    ShowStatus("Stift mit diesem Konto verknüpft.");
                }
                else
                    ShowStatus("Verknüpfung fehlgeschlagen.", isError: true);
            }
            catch (Exception ex)
            {
                ShowStatus($"Fehler: {ex.Message}", isError: true);
            }
        }

        /// <summary>
        /// Hebt die bestehende Verknüpfung zwischen Konto und Stift wieder auf und aktualisiert die Anzeige.
        /// </summary>
        private async void UnlinkPenButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_currentUser?.LinkedPenMacAddress) || _appDataService == null)
                return;
            string mac = _currentUser.LinkedPenMacAddress.Trim();
            try
            {
                bool ok = await _appDataService.UnlinkPenFromUserAsync(mac).ConfigureAwait(true);
                if (ok)
                {
                    _currentUser.LinkedPenMacAddress = null;
                    UpdateLinkedPenInfo();
                    ShowStatus("Verknüpfung aufgehoben.");
                }
                else
                    ShowStatus("Fehler beim Aufheben.", isError: true);
            }
            catch (Exception ex)
            {
                ShowStatus($"Fehler: {ex.Message}", isError: true);
            }
        }

        /// <summary>
        /// Zeigt eine Statusmeldung im Einstellungsbereich an und wählt je nach Fehlerzustand die passende Textfarbe.
        /// </summary>
        /// <param name="message">Anzuzeigender Text.</param>
        /// <param name="isError">True für Fehlerdarstellung (z. B. rote Farbe), sonst normale Sekundärtextfarbe.</param>
        private void ShowStatus(string message, bool isError = false)
        {
            StatusTextBlock.Text = message;
            StatusTextBlock.Visibility = Visibility.Visible;
            StatusTextBlock.Foreground = isError
                ? (System.Windows.Media.Brush)FindResource("StatusErrorBrush")
                : (System.Windows.Media.Brush)FindResource("TextSecondaryBrush");
        }

        /// <summary>
        /// Schließt die Einstellungsansicht, indem das zugehörige Ereignis ausgelöst wird.
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Anzeige-Eintrag für Stift-Combos (MAC-Adresse + Anzeigename für Dropdown und Listen).
        /// </summary>
        private sealed class PenListItem
        {
            /// <summary>MAC-Adresse des Stifts (normalisiert/kanonisch).</summary>
            public string MacAddress { get; }
            /// <summary>Im UI angezeigter Name (z. B. PenName oder DisplayName vom Gerät).</summary>
            public string DisplayLabel { get; set; }

            /// <summary>Erzeugt einen Eintrag für die Stift-Auswahllisten.</summary>
            /// <param name="macAddress">MAC-Adresse des Stifts.</param>
            /// <param name="displayLabel">Anzuzeigende Bezeichnung (Name oder Modell).</param>
            public PenListItem(string macAddress, string displayLabel)
            {
                MacAddress = macAddress ?? string.Empty;
                DisplayLabel = displayLabel ?? macAddress ?? string.Empty;
            }
        }

        /// <summary>
        /// Reagiert auf ScrollChanged: macht die vertikale Scrollbar sichtbar, sobald der Benutzer vertikal scrollt.
        /// </summary>
        /// <param name="sender">Der ScrollViewer (ContentScrollViewer).</param>
        /// <param name="e">Enthält u. a. VerticalChange zur Erkennung vertikaler Bewegung.</param>
        private void OnContentScrollViewerScroll(object sender, ScrollChangedEventArgs e)
        {
            var scrollViewer = (ScrollViewer)sender;
            var verticalScrollBar = scrollViewer.Template?.FindName("PART_VerticalScrollBar", scrollViewer) as ScrollBar;

                if (e.VerticalChange != 0)
                {
                    _scrollBarTimer.Stop();
                    verticalScrollBar.Visibility = Visibility.Visible;
                    _scrollBarTimer.Start();
                    
                }
        }

        /// <summary>
        /// Setzt die vertikale Scrollbar wieder auf hidden, wenn der Benutzer gestoppt hat zu scrollen.
        /// </summary>
        private void OnScrollBarTimerTick(object sender, EventArgs e)
        {

            var verticalScrollBar = ContentScrollViewer.Template?.FindName("PART_VerticalScrollBar", ContentScrollViewer) as ScrollBar;

            if (verticalScrollBar != null && verticalScrollBar.Visibility == Visibility.Visible)
            {
                verticalScrollBar.Visibility = Visibility.Hidden;
                _scrollBarTimer.Stop();
            }
 
        }


    }
}

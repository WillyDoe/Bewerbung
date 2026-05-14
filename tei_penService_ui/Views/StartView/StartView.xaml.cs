using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using tei_penService_ui.Controls;

namespace tei_penService_ui.Views
{
    /// <summary>
    /// Start-Ansicht: Logo, drei Buttons, Tab-Bereich (Verfügbare Geräte / Gekoppelte Geräte).
    /// </summary>
    public partial class StartView : UserControl
    {
        private int _selectedTabIndex;

        /// <summary>
        /// Wird ausgelöst, wenn der Benutzer auf "Abmelden" klickt.
        /// </summary>
        public event EventHandler LogoutRequested;

        /// <summary>
        /// Initialisiert die Start-Ansicht mit Logo, Buttons und Tab-Bereich.
        /// </summary>
        public StartView()
        {
            InitializeComponent();
            Loaded += StartView_Loaded;
        }

        /// <summary>
        /// Setzt die Sichtbarkeit des Abmelden-Buttons abhängig vom Anmeldestatus.
        /// </summary>
        public void SetLoggedIn(bool isLoggedIn)
        {
            var abmeldenButton = FindName("AbmeldenButton") as Button;
            if (abmeldenButton != null)
                abmeldenButton.Visibility = isLoggedIn ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Behandelt Klick auf den Abmelden-Button. Löst LogoutRequested aus.
        /// </summary>
        private void AbmeldenButton_Click(object sender, RoutedEventArgs e)
        {
            LogoutRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Wird beim Laden der StartView aufgerufen. Wendet den Tab-Header-Stil an.
        /// </summary>
        private void StartView_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyTabHeaderStyle();
            if (TabHeaderRow != null)
                TabHeaderRow.SizeChanged += TabHeaderRow_SizeChanged;
        }

        /// <summary>
        /// Wird ausgelöst, wenn die Tab-Header-Zeile eine neue Größe erhält. Aktualisiert Breite und Position der Unterstreichung ohne Animation.
        /// </summary>
        private void TabHeaderRow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateUnderlineWidthAndPosition(animate: false);
        }

        /// <summary>
        /// Index des aktiven Tabs: 0 = Verfügbare Geräte, 1 = Gekoppelte Geräte.
        /// </summary>
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set
            {
                if (_selectedTabIndex == value)
                    return;
                _selectedTabIndex = value;
                ApplyTabSelection();
            }
        }

        /// <summary>
        /// Positioniert Tab-Bereich direkt unter die Button-Zeile.
        /// </summary>
        public void UpdatePenListSectionPosition()
        {
            if (LogoContainer == null || PenListSectionContainer == null)
                return;

            Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    UpdateLayout();
                    var transform = LogoContainer.TransformToAncestor(this);
                    var point = transform.Transform(new Point(0, 0));
                    const int gap = 8;
                    double topMargin = point.Y + LogoContainer.ActualHeight + gap;
                    PenListSectionContainer.Margin = new Thickness(0, topMargin, 0, 0);
                }
                catch
                {
                    // Layout noch nicht bereit
                }
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>
        /// Passt die Breite an die Button-Row an.
        /// </summary>
        public void UpdatePenListWidth()
        {
            if (ButtonContainer == null)
                return;

            Dispatcher.InvokeAsync(() =>
            {
                ButtonContainer.UpdateLayout();
                double buttonWidth = ButtonContainer.ActualWidth;
                if (buttonWidth > 0)
                {
                    if (PenListSectionContainer != null)
                        PenListSectionContainer.Width = buttonWidth;
                    if (TabHeaderRow != null)
                        TabHeaderRow.Width = buttonWidth;
                    if (TabContentArea != null)
                        TabContentArea.Width = buttonWidth;
                    if (ContentAvailable != null)
                        ContentAvailable.Width = buttonWidth;
                    var itemsControlAvailable = ContentAvailable?.Content as ItemsControl;
                    if (itemsControlAvailable != null)
                        itemsControlAvailable.Width = buttonWidth;
                    if (PairedPenListContainer != null)
                        PairedPenListContainer.Width = buttonWidth;
                }
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>
        /// Zeigt den Tab "Verfügbare Geräte" (Index 0).
        /// </summary>
        public void SelectAvailableTab()
        {
            SelectedTabIndex = 0;
        }

        /// <summary>
        /// Zeigt den Tab "Gekoppelte Geräte" (Index 1).
        /// </summary>
        public void SelectPairedTab()
        {
            SelectedTabIndex = 1;
        }

        /// <summary>
        /// Behandelt Klick auf den Tab-Header "Verfügbare Geräte" und wechselt zu diesem Tab.
        /// </summary>
        private void TabHeaderAvailable_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            SelectAvailableTab();
        }

        /// <summary>
        /// Behandelt Klick auf den Tab-Header "Gekoppelte Geräte" und wechselt zu diesem Tab.
        /// </summary>
        private void TabHeaderPaired_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            SelectPairedTab();
        }

        /// <summary>
        /// Hover über "Verfügbare Geräte": Schrift in Tei Cyan; Unterstreichung nur in Tei Cyan, wenn dieser Tab aktuell ausgewählt ist.
        /// </summary>
        private void TabHeaderAvailable_MouseEnter(object sender, MouseEventArgs e)
        {
            var teiCyan = TryFindResource("TeiCyanBrush") as SolidColorBrush;
            if (teiCyan != null)
            {
                if (TabHeaderAvailableText != null)
                    TabHeaderAvailableText.Foreground = teiCyan;
                if (TabUnderline != null && _selectedTabIndex == 0)
                    TabUnderline.Background = teiCyan;
            }
        }

        /// <summary>
        /// Hover über "Gekoppelte Geräte": Schrift in Tei Cyan; Unterstreichung nur in Tei Cyan, wenn dieser Tab aktuell ausgewählt ist.
        /// </summary>
        private void TabHeaderPaired_MouseEnter(object sender, MouseEventArgs e)
        {
            var teiCyan = TryFindResource("TeiCyanBrush") as SolidColorBrush;
            if (teiCyan != null)
            {
                if (TabHeaderPairedText != null)
                    TabHeaderPairedText.Foreground = teiCyan;
                if (TabUnderline != null && _selectedTabIndex == 1)
                    TabUnderline.Background = teiCyan;
            }
        }

        /// <summary>
        /// Maus verlässt einen Tab-Header: Schrift und Unterstreichung wieder auf helles Grau (TextSecondaryBrush) setzen.
        /// </summary>
        private void TabHeader_MouseLeave(object sender, MouseEventArgs e)
        {
            var grayBrush = TryFindResource("TextSecondaryBrush") as SolidColorBrush;
            if (grayBrush != null)
            {
                if (TabHeaderAvailableText != null)
                    TabHeaderAvailableText.Foreground = grayBrush;
                if (TabHeaderPairedText != null)
                    TabHeaderPairedText.Foreground = grayBrush;
                if (TabUnderline != null)
                    TabUnderline.Background = grayBrush;
            }
        }

        /// <summary>
        /// Wendet die Tab-Auswahl an: blendet den gewählten Inhaltsbereich ein und den anderen aus, aktualisiert den Tab-Header-Stil.
        /// </summary>
        private void ApplyTabSelection()
        {
            if (ContentAvailable == null || PairedPenListContainer == null)
                return;

            if (_selectedTabIndex == 0)
            {
                ContentAvailable.Visibility = Visibility.Visible;
                PairedPenListContainer.Visibility = Visibility.Collapsed;
            }
            else
            {
                ContentAvailable.Visibility = Visibility.Collapsed;
                PairedPenListContainer.Visibility = Visibility.Visible;
            }

            ApplyTabHeaderStyle();
        }

        /// <summary>
        /// Unterstreichung als gemeinsamer Balken: Breite/Position setzen, ggf. mit Animation (Tab-Wechsel).
        /// </summary>
        private void ApplyTabHeaderStyle()
        {
            if (TabHeaderAvailable != null)
                TabHeaderAvailable.Background = Brushes.Transparent;
            if (TabHeaderPaired != null)
                TabHeaderPaired.Background = Brushes.Transparent;

            UpdateUnderlineWidthAndPosition(animate: true);
        }

        /// <summary>
        /// Setzt Breite und X-Position der Unterstreichung. Bei animate=true wird die Position animiert (Tab-Wechsel), sonst sofort (z. B. nach Layout/Resize).
        /// </summary>
        private void UpdateUnderlineWidthAndPosition(bool animate)
        {
            if (TabHeaderRow == null || TabUnderline == null || TabUnderlineTransform == null)
                return;

            double totalWidth = TabHeaderRow.ActualWidth;
            if (totalWidth <= 0)
                return;

            double halfWidth = totalWidth * 0.5;
            TabUnderline.Width = halfWidth;
            double targetX = _selectedTabIndex * halfWidth;

            if (animate)
            {
                var animation = new DoubleAnimation
                {
                    To = targetX,
                    Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                animation.Completed += UnderlineAnimation_Completed;
                TabUnderlineTransform.BeginAnimation(TranslateTransform.XProperty, animation);
            }
            else
            {
                TabUnderlineTransform.BeginAnimation(TranslateTransform.XProperty, null);
                TabUnderlineTransform.X = targetX;
            }
        }

        /// <summary>
        /// Wird nach Ende der Unterstreichungs-Animation aufgerufen. Wenn die Maus über dem nun ausgewählten Tab steht, erhalten Unterstreichung und Schrift die Hover-Farbe (Tei Cyan).
        /// </summary>
        private void UnderlineAnimation_Completed(object sender, EventArgs e)
        {
            if (sender is DoubleAnimation anim)
                anim.Completed -= UnderlineAnimation_Completed;

            var teiCyan = TryFindResource("TeiCyanBrush") as SolidColorBrush;
            if (teiCyan == null || TabUnderline == null)
                return;

            bool mouseOverSelectedTab = _selectedTabIndex == 0 && TabHeaderAvailable != null && TabHeaderAvailable.IsMouseOver
                || _selectedTabIndex == 1 && TabHeaderPaired != null && TabHeaderPaired.IsMouseOver;

            if (mouseOverSelectedTab)
            {
                TabUnderline.Background = teiCyan;
                if (_selectedTabIndex == 0 && TabHeaderAvailableText != null)
                    TabHeaderAvailableText.Foreground = teiCyan;
                if (_selectedTabIndex == 1 && TabHeaderPairedText != null)
                    TabHeaderPairedText.Foreground = teiCyan;
            }
        }
    }
}

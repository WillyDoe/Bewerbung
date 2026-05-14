using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace tei_penService_ui.Controls
{
    /// <summary>
    /// Wiederverwendbares Formular-Steuerelement für Passworteingaben mit Bezeichnung (Label).
    /// Zeigt ein maskiertes Eingabefeld; geeignet für Anmelde- und Registrierungsformulare sowie Einstellungen.
    /// </summary>
    public partial class FormularPasswordInputControl : UserControl
    {
        public FormularPasswordInputControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Bezeichner für die Dependency-Property <see cref="Label"/>.
        /// </summary>
        public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
            nameof(Label),
            typeof(string),
            typeof(FormularPasswordInputControl),
            new PropertyMetadata(string.Empty));

        /// <summary>
        /// Bezeichner für die Dependency-Property <see cref="InputWidth"/>.
        /// </summary>
        public static readonly DependencyProperty InputWidthProperty = DependencyProperty.Register(
            nameof(InputWidth),
            typeof(double),
            typeof(FormularPasswordInputControl),
            new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsMeasure));

        /// <summary>
        /// Bezeichner für die Dependency-Property <see cref="FocusedBorderBrush"/>.
        /// </summary>
        public static readonly DependencyProperty FocusedBorderBrushProperty = DependencyProperty.Register(
            nameof(FocusedBorderBrush),
            typeof(SolidColorBrush),
            typeof(FormularPasswordInputControl),
            new PropertyMetadata(null)
        );

        /// <summary>
        /// Bezeichner für die Dependency-Property <see cref="FocusedLabelBrush"/>.
        /// </summary>
        public static readonly DependencyProperty FocusedLabelBrushProperty = DependencyProperty.Register(
            nameof(FocusedLabelBrush),
            typeof(SolidColorBrush),
            typeof(FormularPasswordInputControl),
            new PropertyMetadata(null)
        );
        /// <summary>
        /// Optionale Breite des Eingabefelds (z. B. für kompakte Stift-Passwortfelder). NaN = automatisch.
        /// </summary>
        public double InputWidth
        {
            get => (double)GetValue(InputWidthProperty);
            set => SetValue(InputWidthProperty, value);
        }

        /// <summary>
        /// Bezeichnung (Label), die oberhalb des Passwortfelds angezeigt wird (z. B. "Passwort", "Stift-PIN").
        /// </summary>
        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        /// <summary>
        /// Brush, der verwendet wird, wenn das PasswordInputBox den Fokus hat.
        /// Kann z. B. per DynamicResource auf eine SolidColorBrush-Ressource gesetzt werden.
        /// </summary>
        public SolidColorBrush FocusedBorderBrush
        {
            get => (SolidColorBrush)GetValue(FocusedBorderBrushProperty);
            set => SetValue(FocusedBorderBrushProperty, value);
        }

        /// <summary>
        /// Brush, der für das Label verwendet wird, wenn das PasswordInputBox den Fokus hat.
        /// </summary>
        public SolidColorBrush FocusedLabelBrush
        {
            get => (SolidColorBrush)GetValue(FocusedLabelBrushProperty);
            set => SetValue(FocusedLabelBrushProperty, value);
        }
        /// <summary>
        /// Aktueller Inhalt des Passwortfeldes. Programmgesteuert lesbar/setzbar; nicht per Datenbindung (Sicherheit).
        /// </summary>
        public string Password
        {
            get => PasswordInputBox?.Password ?? string.Empty;
            set { if (PasswordInputBox != null) PasswordInputBox.Password = value ?? string.Empty; }
        }

        /// <summary>
        /// Event Handler, der Border und TextBlock styled, wenn das PasswordInputBox in den Fokus kommt.
        /// </summary>
        private void PasswordInputBox_GotFocus(object sender, RoutedEventArgs e)
        {
            var borderBrush = FocusedBorderBrush ?? Application.Current?.TryFindResource("SettingsControlFocusedBorderBrush") as SolidColorBrush;
            var labelBrush = FocusedLabelBrush ?? Application.Current?.TryFindResource("SettingsControlFocusedLabelBrush") as SolidColorBrush;

            if (borderBrush != null) PasswordInputBorder.BorderBrush = borderBrush;
            if (labelBrush != null) LabelTextBlock.Foreground = labelBrush;
        }

        /// <summary>
        /// Event Handler, der Border und TextBlock styled, wenn das PasswordInputBox den Fokus verliert.
        /// </summary>
        private void PasswordInputBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var borderBrush = Application.Current?.TryFindResource("HellgrauBrush") as SolidColorBrush;
            var labelBrush = Application.Current?.TryFindResource("TextSecondaryBrush") as SolidColorBrush;

            if (borderBrush != null) PasswordInputBorder.BorderBrush = borderBrush;
            if (labelBrush != null) LabelTextBlock.Foreground = labelBrush;
        }
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace tei_penService_ui.Controls
{
    /// <summary>
    /// Wiederverwendbares Formular-Steuerelement mit Bezeichnung und einzeiligem Texteingabefeld.
    /// Wird in Formularen (z. B. Login, Registrierung, Einstellungen) für Werte wie Benutzername, E-Mail o. Ä. verwendet.
    /// </summary>
    public partial class FormularTextInputControl : UserControl
    {
        public FormularTextInputControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Bezeichner für die Dependency-Property <see cref="Label"/>.
        /// </summary>
        public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
            nameof(Label),
            typeof(string),
            typeof(FormularTextInputControl),
            new PropertyMetadata(string.Empty)
        );

        /// <summary>
        /// Bezeichner für die Dependency-Property <see cref="Text"/>.
        /// </summary>
        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(FormularTextInputControl),
            new PropertyMetadata(string.Empty)
        );

        /// <summary>
        /// Bezeichner für die Dependency-Property <see cref="FocusedBorderBrush"/>.
        /// </summary>
        public static readonly DependencyProperty FocusedBorderBrushProperty = DependencyProperty.Register(
            nameof(FocusedBorderBrush),
            typeof(SolidColorBrush),
            typeof(FormularTextInputControl),
            new PropertyMetadata(null)
        );

        /// <summary>
        /// Bezeichner für die Dependency-Property <see cref="FocusedLabelBrush"/>.
        /// </summary>
        public static readonly DependencyProperty FocusedLabelBrushProperty = DependencyProperty.Register(
            nameof(FocusedLabelBrush),
            typeof(SolidColorBrush),
            typeof(FormularTextInputControl),
            new PropertyMetadata(null)
        );
        /// <summary>
        /// Bezeichnung (Label), die oberhalb des Texteingabefelds angezeigt wird.
        /// Z. B. "Benutzername", "E-Mail" oder "Anzeigename".
        /// </summary>
        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        /// <summary>
        /// Der im Texteingabefeld angezeigte bzw. vom Benutzer eingegebene Text.
        /// Kann per Datenbindung (z. B. Two-Way) mit dem ViewModel verbunden werden.
        /// </summary>
        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        /// <summary>
        /// Brush, der verwendet wird, wenn das Input-Element (TextBox) den Fokus hat.
        /// Kann z. B. per DynamicResource auf eine SolidColorBrush-Ressource gesetzt werden.
        /// </summary>
        public SolidColorBrush FocusedBorderBrush
        {
            get => (SolidColorBrush)GetValue(FocusedBorderBrushProperty);
            set => SetValue(FocusedBorderBrushProperty, value);
        }

        /// <summary>
        /// Brush, der für das Label verwendet wird, wenn das Input-Element (TextBox) den Fokus hat.
        /// </summary>
        public SolidColorBrush FocusedLabelBrush
        {
            get => (SolidColorBrush)GetValue(FocusedLabelBrushProperty);
            set => SetValue(FocusedLabelBrushProperty, value);
        }
        /// <summary>
        /// Event Handler, der Border und TextBlock styled, wenn das TextInputTextBox in den Fokus kommt.
        /// </summary>
        private void TextInputTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            var borderBrush = FocusedBorderBrush ?? Application.Current?.TryFindResource("SettingsControlFocusedBorderBrush") as SolidColorBrush;
            var labelBrush = FocusedLabelBrush ?? Application.Current?.TryFindResource("SettingsControlFocusedLabelBrush") as SolidColorBrush;

            if (borderBrush != null) TextInputBorder.BorderBrush = borderBrush;
            if (labelBrush != null) LabelTextBlock.Foreground = labelBrush;
        }

        /// <summary>
        /// Event Handler, der Border und TextBlock styled, wenn das TextInputTextBox den Fokus verliert.
        /// </summary>
        private void TextInputTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var borderBrush = Application.Current?.TryFindResource("HellgrauBrush") as SolidColorBrush;
            var labelBrush = Application.Current?.TryFindResource("TextSecondaryBrush") as SolidColorBrush;

            if (borderBrush != null) TextInputBorder.BorderBrush = borderBrush;
            if (labelBrush != null) LabelTextBlock.Foreground = labelBrush;
        }
    }
}

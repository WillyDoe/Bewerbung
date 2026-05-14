using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace tei_penService_ui.Controls
{
    /// <summary>
    /// Interaktionslogik für StartWindowButton.xaml
    /// </summary>
    public partial class StartWindowButton : UserControl
    {
        /// <summary>
        /// RoutedEvent für Button-Clicks
        /// </summary>
        public static readonly RoutedEvent ButtonClickEvent =
            EventManager.RegisterRoutedEvent(
                nameof(ButtonClick),
                RoutingStrategy.Bubble,
                typeof(RoutedEventHandler),
                typeof(StartWindowButton));

        /// <summary>
        /// DependencyProperty für Icon-Path-Daten
        /// </summary>
        public static readonly DependencyProperty IconPathDataProperty =
            DependencyProperty.Register(
                nameof(IconPathData),
                typeof(string),
                typeof(StartWindowButton),
                new PropertyMetadata(string.Empty));

        /// <summary>
        /// DependencyProperty für Button-Text
        /// </summary>
        public static readonly DependencyProperty ButtonTextProperty =
            DependencyProperty.Register(
                nameof(ButtonText),
                typeof(string),
                typeof(StartWindowButton),
                new PropertyMetadata(string.Empty));

        /// <summary>
        /// DependencyProperty für Button-Tag (für bedingten Hover-Effekt)
        /// </summary>
        public static readonly DependencyProperty ButtonTagProperty =
            DependencyProperty.Register(
                nameof(ButtonTag),
                typeof(object),
                typeof(StartWindowButton),
                new PropertyMetadata(null, OnButtonTagChanged));

        /// <summary>
        /// DependencyProperty für Hover-Foreground-Brush (optional, überschreibt Standard-Hover-Farbe)
        /// </summary>
        public static readonly DependencyProperty HoverForegroundBrushProperty =
            DependencyProperty.Register(
                nameof(HoverForegroundBrush),
                typeof(Brush),
                typeof(StartWindowButton),
                new PropertyMetadata(null));

        /// <summary>
        /// DependencyProperty für Background-Brush
        /// </summary>
        public static readonly DependencyProperty BackgroundBrushProperty =
            DependencyProperty.Register(
                nameof(BackgroundBrush),
                typeof(Brush),
                typeof(StartWindowButton),
                new PropertyMetadata(null));

        /// <summary>
        /// DependencyProperty für Foreground-Brush
        /// </summary>
        public static readonly DependencyProperty ForegroundBrushProperty =
            DependencyProperty.Register(
                nameof(ForegroundBrush),
                typeof(Brush),
                typeof(StartWindowButton),
                new PropertyMetadata(null));

        /// <summary>
        /// Event für Button-Clicks
        /// </summary>
        public event RoutedEventHandler ButtonClick
        {
            add { AddHandler(ButtonClickEvent, value); }
            remove { RemoveHandler(ButtonClickEvent, value); }
        }

        /// <summary>
        /// Path-Geometrie-Daten für das Icon
        /// </summary>
        public string IconPathData
        {
            get => (string)GetValue(IconPathDataProperty);
            set => SetValue(IconPathDataProperty, value);
        }

        /// <summary>
        /// Text des Buttons
        /// </summary>
        public string ButtonText
        {
            get => (string)GetValue(ButtonTextProperty);
            set => SetValue(ButtonTextProperty, value);
        }

        /// <summary>
        /// Tag des internen Buttons (für bedingten Hover-Effekt)
        /// </summary>
        public object ButtonTag
        {
            get => GetValue(ButtonTagProperty);
            set => SetValue(ButtonTagProperty, value);
        }

        /// <summary>
        /// Brush für Hover-Foreground (optional, überschreibt Standard-Hover-Farbe)
        /// </summary>
        public Brush HoverForegroundBrush
        {
            get => (Brush)GetValue(HoverForegroundBrushProperty);
            set => SetValue(HoverForegroundBrushProperty, value);
        }

        /// <summary>
        /// Brush für Background
        /// </summary>
        public Brush BackgroundBrush
        {
            get => (Brush)GetValue(BackgroundBrushProperty);
            set => SetValue(BackgroundBrushProperty, value);
        }

        /// <summary>
        /// Brush für Foreground (Text und Icon)
        /// </summary>
        public Brush ForegroundBrush
        {
            get => (Brush)GetValue(ForegroundBrushProperty);
            set => SetValue(ForegroundBrushProperty, value);
        }

        /// <summary>
        /// Interner Button für Tag-Zugriff
        /// </summary>
        public Button InternalButtonControl => InternalButton;

        /// <summary>
        /// Initialisiert den Start-Window-Button und registriert den Loaded-Handler.
        /// </summary>
        public StartWindowButton()
        {
            InitializeComponent();
            Loaded += StartWindowButton_Loaded;
        }

        /// <summary>
        /// Kopiert beim Laden das ButtonTag auf den internen Button für bedingte Hover-Logik.
        /// </summary>
        private void StartWindowButton_Loaded(object sender, RoutedEventArgs e)
        {
            // ButtonTag auf internen Button kopieren, falls gesetzt
            if (InternalButton != null && ButtonTag != null)
            {
                InternalButton.Tag = ButtonTag;
            }
        }

        private static void OnButtonTagChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is StartWindowButton control && control.InternalButton != null)
            {
                control.InternalButton.Tag = e.NewValue;
            }
        }

        /// <summary>
        /// Leitet Klicks auf den internen Button als ButtonClick-RoutedEvent nach außen weiter.
        /// </summary>
        private void InternalButton_Click(object sender, RoutedEventArgs e)
        {
            // Event nach außen weiterleiten
            RoutedEventArgs newEventArgs = new RoutedEventArgs(ButtonClickEvent, this);
            RaiseEvent(newEventArgs);
        }
    }
}

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace tei_penService_ui.Controls
{
    /// <summary>
    /// Wiederverwendbarer Button mit Ladekreis-Effekt für Aktionen, die Zeit benötigen (z. B. Verbinden, Trennen, Entfernen).
    /// </summary>
    public partial class LoadingButton : UserControl
    {
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(
                nameof(Text),
                typeof(string),
                typeof(LoadingButton),
                new PropertyMetadata(string.Empty, OnTextChanged));

        public static readonly DependencyProperty IsLoadingProperty =
            DependencyProperty.Register(
                nameof(IsLoading),
                typeof(bool),
                typeof(LoadingButton),
                new PropertyMetadata(false, OnIsLoadingChanged));

        /// <summary>
        /// Ressourcenschlüssel für den Akzent-Brush (z. B. TeiGreenBrush, TeiYellowBrush, StatusErrorBrush).
        /// Wird für Hover und Spinner-Farbe verwendet.
        /// </summary>
        public static readonly DependencyProperty AccentBrushKeyProperty =
            DependencyProperty.Register(
                nameof(AccentBrushKey),
                typeof(string),
                typeof(LoadingButton),
                new PropertyMetadata("TeiGreenBrush", OnAccentBrushKeyChanged));

        /// <summary>
        /// Abstand zwischen Rahmen und Inhalt (Text/Spinner). Je nach Anwendungsort setzen (z. B. "12,4" für Pen-Controls).
        /// </summary>
        public static readonly DependencyProperty ContentPaddingProperty =
            DependencyProperty.Register(
                nameof(ContentPadding),
                typeof(Thickness),
                typeof(LoadingButton),
                new PropertyMetadata(new Thickness(12, 4, 12, 4), OnContentPaddingChanged));

        private static void OnContentPaddingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LoadingButton c)
                c.ApplyContentPadding((Thickness)e.NewValue);
        }

        private void ApplyContentPadding(Thickness padding)
        {
            var border = FindName("ButtonBorder") as Border;
            if (border != null)
                border.Padding = padding;
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LoadingButton c && c.ButtonTextBlock != null)
                c.ButtonTextBlock.Text = (string)e.NewValue;
        }

        private static void OnIsLoadingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LoadingButton c)
                c.ApplyLoadingState((bool)e.NewValue);
        }

        private static void OnAccentBrushKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LoadingButton c && !c.IsLoading)
                c.ApplyAccentToBorderAndText();
        }

        /// <summary>
        /// Wird ausgelöst, wenn der Button geklickt wird (nur wenn nicht IsLoading).
        /// </summary>
        public event RoutedEventHandler Click;

        public LoadingButton()
        {
            InitializeComponent();
            Loaded += LoadingButton_Loaded;
        }

        private void LoadingButton_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyContentPadding(ContentPadding);
            if (ButtonTextBlock != null)
                ButtonTextBlock.Text = Text;
            ApplyLoadingState(IsLoading);
        }

        private const double ContentAreaMinHeight = 16;

        protected override Size MeasureOverride(Size constraint)
        {
            Size baseSize = base.MeasureOverride(constraint);
            double minW = Math.Max(MinWidth, ContentPadding.Left + ContentPadding.Right + 50);
            double minH = Math.Max(20, ContentPadding.Top + ContentPadding.Bottom + ContentAreaMinHeight);
            double w = Math.Max(baseSize.Width, minW);
            double h = Math.Max(baseSize.Height, minH);
            if (!double.IsPositiveInfinity(constraint.Height) && constraint.Height > 0)
                h = Math.Min(h, constraint.Height);
            if (MaxHeight > 0 && !double.IsNaN(MaxHeight) && !double.IsPositiveInfinity(MaxHeight))
                h = Math.Min(h, MaxHeight);
            return new Size(w, h);
        }

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public bool IsLoading
        {
            get => (bool)GetValue(IsLoadingProperty);
            set => SetValue(IsLoadingProperty, value);
        }

        public string AccentBrushKey
        {
            get => (string)GetValue(AccentBrushKeyProperty);
            set => SetValue(AccentBrushKeyProperty, value);
        }

        /// <summary>
        /// Abstand zwischen Button-Rahmen und Inhalt. Pro Anwendungsort setzbar (z. B. 12,4 für Pen-Controls).
        /// </summary>
        public Thickness ContentPadding
        {
            get => (Thickness)GetValue(ContentPaddingProperty);
            set => SetValue(ContentPaddingProperty, value);
        }

        private void ApplyLoadingState(bool loading)
        {
            if (ButtonTextBlock == null || LoadingIndicator == null)
                return;

            if (loading)
            {
                ButtonTextBlock.Visibility = Visibility.Collapsed;
                LoadingIndicator.Visibility = Visibility.Visible;
                StartSpinnerAnimation();
                ApplyAccentToBorderAndText();
                if (SpinnerEllipse != null)
                {
                    var brush = TryFindBrush(AccentBrushKey);
                    if (brush != null)
                        SpinnerEllipse.Stroke = brush;
                }
            }
            else
            {
                StopSpinnerAnimation();
                LoadingIndicator.Visibility = Visibility.Collapsed;
                ButtonTextBlock.Visibility = Visibility.Visible;
                ApplyAccentToBorderAndText();
            }
        }

        private void StartSpinnerAnimation()
        {
            if (SpinnerRotate == null)
                return;
            var anim = new DoubleAnimation(0, 360, new Duration(TimeSpan.FromSeconds(1.2)))
            {
                RepeatBehavior = RepeatBehavior.Forever
            };
            SpinnerRotate.BeginAnimation(RotateTransform.AngleProperty, anim);
        }

        private void StopSpinnerAnimation()
        {
            if (SpinnerRotate != null)
                SpinnerRotate.BeginAnimation(RotateTransform.AngleProperty, null);
        }

        private void ButtonBorder_MouseEnter(object sender, MouseEventArgs e)
        {
            var border = (Border)sender;
            var hoverBg = TryFindBrush("TitleBarHoverBrush");
            if (hoverBg != null)
                border.Background = hoverBg;
            ApplyAccentToBorderAndText();
        }

        private void ButtonBorder_MouseLeave(object sender, MouseEventArgs e)
        {
            var border = (Border)sender;
            border.Background = Brushes.Transparent;
            ApplyAccentToBorderAndText();
        }

        private void ButtonBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsLoading)
            {
                e.Handled = true;
                return;
            }
            Click?.Invoke(this, new RoutedEventArgs());
        }

        private void ApplyAccentToBorderAndText()
        {
            var border = ButtonBorder;
            var textBlock = ButtonTextBlock;
            if (border == null || textBlock == null)
                return;

            var accent = TryFindBrush(AccentBrushKey);
            var defaultBorder = new SolidColorBrush(Color.FromArgb(0x4D, 0, 0, 0));
            var textBrush = TryFindBrush("TextBrush");

            if (IsLoading)
            {
                if (accent != null)
                {
                    border.BorderBrush = accent;
                    if (SpinnerEllipse != null)
                        SpinnerEllipse.Stroke = accent;
                }
                return;
            }

            bool isHover = border.IsMouseOver;
            if (isHover && accent != null)
            {
                border.BorderBrush = accent;
                textBlock.Foreground = accent;
            }
            else
            {
                border.BorderBrush = defaultBorder;
                textBlock.Foreground = textBrush ?? Brushes.Black;
            }
        }

        private static SolidColorBrush TryFindBrush(string key)
        {
            return Application.Current?.TryFindResource(key) as SolidColorBrush;
        }
    }
}

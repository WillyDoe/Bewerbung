using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace tei_penService_ui.Controls
{
    /// <summary>
    /// Wiederverwendbarer Container für Einstellungsblöcke: optionales SubHeading,
    /// dynamische Anzahl Input-Elemente und ein oder mehrere Buttons mit konfigurierbaren Abständen.
    /// </summary>
    public partial class SettingsBlockContainer : UserControl
    {
        /// <summary>
        /// Setzt den logischen Parent eines Elements per Reflection auf null, falls der Parser
        /// ihn ohne AddLogicalChild gesetzt hat und RemoveLogicalChild daher nicht greift.
        /// </summary>
        private static void ClearLogicalParent(FrameworkElement element)
        {
            if (element == null) return;
            try
            {
                var type = element.GetType();
                while (type != null)
                {
                    var field = type.GetField("_parent", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        field.SetValue(element, null);
                        return;
                    }
                    type = type.BaseType;
                }
            }
            catch
            {
                // Reflection fehlgeschlagen; RemoveLogicalChild reicht ggf. aus
            }
        }
        public static readonly DependencyProperty SubHeadingProperty =
            DependencyProperty.Register(
                nameof(SubHeading),
                typeof(object),
                typeof(SettingsBlockContainer),
                new PropertyMetadata(null, OnSubHeadingChanged));

        public static readonly DependencyProperty SubHeadingToInputMarginProperty =
            DependencyProperty.Register(
                nameof(SubHeadingToInputMargin),
                typeof(double),
                typeof(SettingsBlockContainer),
                new PropertyMetadata(0.0, OnSubHeadingMarginChanged));

        public static readonly DependencyProperty InputsProperty =
            DependencyProperty.Register(
                nameof(Inputs),
                typeof(ObservableCollection<UIElement>),
                typeof(SettingsBlockContainer),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure, OnInputsPropertyChanged));

        public static readonly DependencyProperty InputSpacingProperty =
            DependencyProperty.Register(
                nameof(InputSpacing),
                typeof(double),
                typeof(SettingsBlockContainer),
                new PropertyMetadata(8.0, OnInputSpacingChanged));

        public static readonly DependencyProperty InputToButtonMarginProperty =
            DependencyProperty.Register(
                nameof(InputToButtonMargin),
                typeof(double),
                typeof(SettingsBlockContainer),
                new PropertyMetadata(16.0, OnInputToButtonMarginChanged));

        public static readonly DependencyProperty ButtonsProperty =
            DependencyProperty.Register(
                nameof(Buttons),
                typeof(ObservableCollection<UIElement>),
                typeof(SettingsBlockContainer),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure, OnButtonsPropertyChanged));

        public static readonly DependencyProperty ButtonOrientationProperty =
            DependencyProperty.Register(
                nameof(ButtonOrientation),
                typeof(Orientation),
                typeof(SettingsBlockContainer),
                new PropertyMetadata(Orientation.Horizontal, OnButtonOrientationChanged));

        public static readonly DependencyProperty ButtonSpacingProperty =
            DependencyProperty.Register(
                nameof(ButtonSpacing),
                typeof(double),
                typeof(SettingsBlockContainer),
                new PropertyMetadata(8.0, OnButtonSpacingChanged));

        public static readonly DependencyProperty BottomMarginProperty =
            DependencyProperty.Register(
                nameof(BottomMargin),
                typeof(double),
                typeof(SettingsBlockContainer),
                new PropertyMetadata(0.0, OnBottomMarginChanged));

        /// <summary>
        /// Optionales Überschrift-Element (z. B. TextBlock mit SettingsSubHeadingStyle).
        /// Wenn <c>null</c>, wird der SubHeading-Bereich ausgeblendet.
        /// </summary>
        public object SubHeading
        {
            get => GetValue(SubHeadingProperty);
            set => SetValue(SubHeadingProperty, value);
        }

        /// <summary>
        /// Abstand in Pixel zwischen SubHeading und dem ersten Input-Element.
        /// </summary>
        public double SubHeadingToInputMargin
        {
            get => (double)GetValue(SubHeadingToInputMarginProperty);
            set => SetValue(SubHeadingToInputMarginProperty, value);
        }

        /// <summary>
        /// Sammlung der Input-Elemente (z. B. TextInput, ComboBox, PasswordInput).
        /// Wird bei <c>null</c> beim ersten Zugriff automatisch initialisiert.
        /// </summary>
        public ObservableCollection<UIElement> Inputs
        {
            get
            {
                var col = (ObservableCollection<UIElement>)GetValue(InputsProperty);
                if (col == null)
                {
                    col = new ObservableCollection<UIElement>();
                    SetCurrentValue(InputsProperty, col);
                }
                return col;
            }
            set => SetValue(InputsProperty, value);
        }

        /// <summary>
        /// Einheitlicher Abstand in Pixel zwischen den Input-Elementen.
        /// </summary>
        public double InputSpacing
        {
            get => (double)GetValue(InputSpacingProperty);
            set => SetValue(InputSpacingProperty, value);
        }

        /// <summary>
        /// Abstand in Pixel zwischen dem letzten Input-Element und dem Button-Bereich.
        /// Wird auch als Abstand oberhalb der Buttons genutzt, wenn keine Inputs vorhanden sind.
        /// </summary>
        public double InputToButtonMargin
        {
            get => (double)GetValue(InputToButtonMarginProperty);
            set => SetValue(InputToButtonMarginProperty, value);
        }

        /// <summary>
        /// Sammlung der Button-Elemente (z. B. Speichern, Abbrechen).
        /// Wird bei <c>null</c> beim ersten Zugriff automatisch initialisiert.
        /// </summary>
        public ObservableCollection<UIElement> Buttons
        {
            get
            {
                var col = (ObservableCollection<UIElement>)GetValue(ButtonsProperty);
                if (col == null)
                {
                    col = new ObservableCollection<UIElement>();
                    SetCurrentValue(ButtonsProperty, col);
                }
                return col;
            }
            set => SetValue(ButtonsProperty, value);
        }

        /// <summary>
        /// Anordnung der Buttons: horizontal nebeneinander oder vertikal untereinander.
        /// </summary>
        public Orientation ButtonOrientation
        {
            get => (Orientation)GetValue(ButtonOrientationProperty);
            set => SetValue(ButtonOrientationProperty, value);
        }

        /// <summary>
        /// Abstand in Pixel zwischen den Buttons (rechts bei Horizontal, unten bei Vertical).
        /// </summary>
        public double ButtonSpacing
        {
            get => (double)GetValue(ButtonSpacingProperty);
            set => SetValue(ButtonSpacingProperty, value);
        }

        /// <summary>
        /// Abstand in Pixel unter dem gesamten Block (unter dem letzten Button).
        /// </summary>
        public double BottomMargin
        {
            get => (double)GetValue(BottomMarginProperty);
            set => SetValue(BottomMarginProperty, value);
        }

        /// <summary>
        /// Initialisiert die Control und registriert den <see cref="Loaded"/>-Handler
        /// zum ersten Aufbau von SubHeading, Inputs und Buttons. Die Sammlungen
        /// <see cref="Inputs"/> und <see cref="Buttons"/> werden vor dem Laden des XAML
        /// gesetzt, damit der XAML-Parser sie befüllen kann.
        /// </summary>
        public SettingsBlockContainer()
        {
            SetCurrentValue(InputsProperty, new ObservableCollection<UIElement>());
            SetCurrentValue(ButtonsProperty, new ObservableCollection<UIElement>());
            InitializeComponent();
            Loaded += OnLoaded;
        }

        /// <summary>
        /// Wird beim ersten Laden der Control ausgeführt. Baut SubHeading, Input-Panel
        /// und Button-Panel einmalig auf, damit die aus XAML gesetzten Inhalte angezeigt werden.
        /// </summary>
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateSubHeadingPresenter();
            RebuildInputsPanel();
            RebuildButtonsPanel();
        }

        /// <summary>
        /// Callback bei Änderung der <see cref="SubHeading"/>-Property.
        /// Aktualisiert die Darstellung des SubHeading-Bereichs.
        /// </summary>
        private static void OnSubHeadingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((SettingsBlockContainer)d).UpdateSubHeadingPresenter();
        }

        /// <summary>
        /// Callback bei Änderung der <see cref="SubHeadingToInputMargin"/>-Property.
        /// Passt den unteren Rand des SubHeading-Bereichs an.
        /// </summary>
        private static void OnSubHeadingMarginChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((SettingsBlockContainer)d).UpdateSubHeadingPresenter();
        }

        /// <summary>
        /// Aktualisiert den SubHeading-<see cref="ContentPresenter"/>: Zeigt den Inhalt von
        /// <see cref="SubHeading"/> an oder blendet den Bereich aus, wenn <see cref="SubHeading"/> <c>null</c> ist.
        /// Setzt außerdem den unteren Rand auf <see cref="SubHeadingToInputMargin"/>.
        /// </summary>
        private void UpdateSubHeadingPresenter()
        {
            if (SubHeadingPresenter == null)
                return;

            if (SubHeading == null)
            {
                SubHeadingPresenter.Visibility = Visibility.Collapsed;
                SubHeadingPresenter.Content = null;
                return;
            }

            SubHeadingPresenter.Visibility = Visibility.Visible;
            SubHeadingPresenter.Content = SubHeading;
            SubHeadingPresenter.Margin = new Thickness(0, 0, 0, SubHeadingToInputMargin);
        }

        /// <summary>
        /// Callback bei Änderung der <see cref="Inputs"/>-Property. Heftet sich von der alten
        /// Collection ab und an die neue an (<see cref="INotifyCollectionChanged"/>), und baut das Input-Panel neu auf.
        /// </summary>
        private static void OnInputsPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var c = (SettingsBlockContainer)d;
            if (e.OldValue is INotifyCollectionChanged oldCol)
                oldCol.CollectionChanged -= c.OnInputsCollectionChanged;
            if (e.NewValue is INotifyCollectionChanged newCol)
            {
                newCol.CollectionChanged += c.OnInputsCollectionChanged;
                c.RebuildInputsPanel();
            }
        }

        /// <summary>
        /// Callback bei Änderung der <see cref="InputSpacing"/>-Property.
        /// Baut das Input-Panel mit dem neuen Abstand zwischen den Elementen neu auf.
        /// </summary>
        private static void OnInputSpacingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((SettingsBlockContainer)d).RebuildInputsPanel();
        }

        /// <summary>
        /// Callback bei Änderung der <see cref="InputToButtonMargin"/>-Property.
        /// Aktualisiert Input-Panel und den oberen Rand des Button-Bereichs.
        /// </summary>
        private static void OnInputToButtonMarginChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var c = (SettingsBlockContainer)d;
            c.RebuildInputsPanel();
            c.UpdateButtonsPanelMargin();
        }

        /// <summary>
        /// Wird ausgelöst, wenn sich die <see cref="Inputs"/>-Collection ändert (Add/Remove/etc.).
        /// Baut das Input-Panel nur nach dem Laden neu auf, um Konflikte mit dem XAML-Parser zu vermeiden.
        /// </summary>
        private void OnInputsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (IsLoaded)
                RebuildInputsPanel();
        }

        /// <summary>
        /// Baut das Input-Panel vollständig neu auf: Leert <see cref="InputsPanel"/>, fügt jedes Element
        /// aus <see cref="Inputs"/> in ein <see cref="Border"/> mit passender Margin ein (zwischen den Elementen
        /// <see cref="InputSpacing"/>, beim letzten Element <see cref="InputToButtonMargin"/>). Die Collection
        /// wird temporär geleert, damit WPF die logischen Kinder trennt, danach werden die Referenzen wieder eingetragen.
        /// </summary>
        private void RebuildInputsPanel()
        {
            if (InputsPanel == null)
                return;

            var inputs = Inputs;
            if (inputs == null)
                return;

            InputsPanel.Children.Clear();

            var count = inputs.Count;
            if (count == 0)
            {
                UpdateButtonsPanelMargin();
                return;
            }

            var spacing = InputSpacing;
            var toButton = InputToButtonMargin;
            var list = new List<UIElement>(count);
            for (var i = 0; i < count; i++)
                list.Add(inputs[i]);

            if (inputs is INotifyCollectionChanged notifier)
                notifier.CollectionChanged -= OnInputsCollectionChanged;
            try
            {
                inputs.Clear();
                for (var i = 0; i < count; i++)
                {
                    var child = list[i];
                    RemoveLogicalChild(child);
                    if (child is FrameworkElement fe)
                        ClearLogicalParent(fe);
                    var isLast = i == count - 1;
                    var margin = isLast
                        ? new Thickness(0, 0, 0, toButton)
                        : new Thickness(0, 0, 0, spacing);
                    var wrapper = new Border { Child = child, Margin = margin };

                    // Visibility des Wrapper-Borders an den Visibility-Status des Child-Elements binden
                    BindingOperations.SetBinding(wrapper, UIElement.VisibilityProperty, new Binding("Visibility") { Source = child });

                    InputsPanel.Children.Add(wrapper);
                }
                for (var i = 0; i < count; i++)
                    inputs.Add(list[i]);
            }
            finally
            {
                if (inputs is INotifyCollectionChanged n)
                    n.CollectionChanged += OnInputsCollectionChanged;
            }

            UpdateButtonsPanelMargin();
        }

        /// <summary>
        /// Callback bei Änderung der <see cref="Buttons"/>-Property. Heftet sich von der alten
        /// Collection ab und an die neue an, und baut das Button-Panel neu auf.
        /// </summary>
        private static void OnButtonsPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var c = (SettingsBlockContainer)d;
            if (e.OldValue is INotifyCollectionChanged oldCol)
                oldCol.CollectionChanged -= c.OnButtonsCollectionChanged;
            if (e.NewValue is INotifyCollectionChanged newCol)
            {
                newCol.CollectionChanged += c.OnButtonsCollectionChanged;
                c.RebuildButtonsPanel();
            }
        }

        /// <summary>
        /// Callback bei Änderung der <see cref="ButtonOrientation"/>-Property.
        /// Setzt die <see cref="StackPanel.Orientation"/> des Button-Panels auf Horizontal oder Vertical.
        /// </summary>
        private static void OnButtonOrientationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var c = (SettingsBlockContainer)d;
            if (c.ButtonsPanel != null)
                c.ButtonsPanel.Orientation = (Orientation)e.NewValue;
        }

        /// <summary>
        /// Callback bei Änderung der <see cref="ButtonSpacing"/>-Property.
        /// Baut das Button-Panel mit dem neuen Abstand zwischen den Buttons neu auf.
        /// </summary>
        private static void OnButtonSpacingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((SettingsBlockContainer)d).RebuildButtonsPanel();
        }

        /// <summary>
        /// Callback bei Änderung der <see cref="BottomMargin"/>-Property.
        /// Aktualisiert den unteren Rand des Button-Bereichs.
        /// </summary>
        private static void OnBottomMarginChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((SettingsBlockContainer)d).UpdateButtonsPanelMargin();
        }

        /// <summary>
        /// Wird ausgelöst, wenn sich die <see cref="Buttons"/>-Collection ändert.
        /// Baut das Button-Panel nur nach dem Laden neu auf, um Konflikte mit dem XAML-Parser zu vermeiden.
        /// </summary>
        private void OnButtonsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (IsLoaded)
                RebuildButtonsPanel();
        }

        /// <summary>
        /// Setzt die Margin des Button-Panels: oben <see cref="InputToButtonMargin"/>, wenn keine
        /// Inputs vorhanden sind (Abstand zwischen SubHeading und Buttons), sonst 0; unten <see cref="BottomMargin"/>.
        /// </summary>
        private void UpdateButtonsPanelMargin()
        {
            if (ButtonsPanel == null)
                return;
            var hasInputs = Inputs?.Count > 0;
            ButtonsPanel.Margin = new Thickness(0, hasInputs ? 0 : InputToButtonMargin, 0, BottomMargin);
        }

        /// <summary>
        /// Baut das Button-Panel vollständig neu auf: setzt <see cref="ButtonOrientation"/> und Margin,
        /// leert die Children und fügt jedes Element aus <see cref="Buttons"/> in ein <see cref="Border"/>
        /// mit passender Margin ein. Die Collection wird temporär geleert, damit WPF die logischen Kinder trennt.
        /// </summary>
        private void RebuildButtonsPanel()
        {
            if (ButtonsPanel == null)
                return;

            var buttons = Buttons;
            if (buttons == null)
                return;

            ButtonsPanel.Orientation = ButtonOrientation;
            UpdateButtonsPanelMargin();
            ButtonsPanel.Children.Clear();

            var count = buttons.Count;
            if (count == 0)
                return;

            var spacing = ButtonSpacing;
            var isHorizontal = ButtonOrientation == Orientation.Horizontal;
            var list = new List<UIElement>(count);
            for (var i = 0; i < count; i++)
                list.Add(buttons[i]);

            if (buttons is INotifyCollectionChanged notifier)
                notifier.CollectionChanged -= OnButtonsCollectionChanged;
            try
            {
                buttons.Clear();
                for (var i = 0; i < count; i++)
                {
                    var child = list[i];
                    RemoveLogicalChild(child);
                    if (child is FrameworkElement fe)
                        ClearLogicalParent(fe);
                    var isLast = i == count - 1;
                    Thickness margin;
                    if (isHorizontal)
                        margin = isLast ? new Thickness(0) : new Thickness(0, 0, spacing, 0);
                    else
                        margin = isLast ? new Thickness(0) : new Thickness(0, 0, 0, spacing);
                    var wrapper = new Border { Child = child, Margin = margin };
                    ButtonsPanel.Children.Add(wrapper);
                }
                for (var i = 0; i < count; i++)
                    buttons.Add(list[i]);
            }
            finally
            {
                if (buttons is INotifyCollectionChanged n)
                    n.CollectionChanged += OnButtonsCollectionChanged;
            }
        }
    }
}

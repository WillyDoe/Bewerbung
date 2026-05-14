using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System;
using System.Windows.Controls.Primitives;
namespace tei_penService_ui.Controls
{
    /// <summary>
    /// Wiederverwendbares Einstellungs-Steuerelement: ComboBox mit Bezeichnung (Label) im einheitlichen Settings-Stil.
    /// Eignet sich für Auswahlfelder wie Sprache, Theme oder andere Konfigurationsoptionen.
    /// </summary>
    public partial class SettingsComboBoxControl : UserControl
    {
        public SettingsComboBoxControl()
        {
            InitializeComponent();
            InnerComboBox.SelectionChanged += (s, e) => SelectionChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Wird ausgelöst, wenn sich die Auswahl der inneren ComboBox ändert.
        /// </summary>
        public event SelectionChangedEventHandler SelectionChanged;

        /// <summary>
        /// Bezeichner für die Dependency-Property <see cref="Label"/>.
        /// </summary>
        public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
            nameof(Label),
            typeof(string),
            typeof(SettingsComboBoxControl),
            new PropertyMetadata(string.Empty));

        /// <summary>
        /// Bezeichner für die Dependency-Property <see cref="ItemsSource"/>.
        /// </summary>
        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable),
            typeof(SettingsComboBoxControl),
            new PropertyMetadata(null));

        /// <summary>
        /// Bezeichner für die Dependency-Property <see cref="SelectedItem"/>.
        /// </summary>
        public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(
            nameof(SelectedItem),
            typeof(object),
            typeof(SettingsComboBoxControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        /// <summary>
        /// Bezeichner für die Dependency-Property <see cref="DisplayMemberPath"/>.
        /// </summary>
        public static readonly DependencyProperty DisplayMemberPathProperty = DependencyProperty.Register(
            nameof(DisplayMemberPath),
            typeof(string),
            typeof(SettingsComboBoxControl),
            new PropertyMetadata("DisplayLabel"));

        /// <summary>
        /// Bezeichnung (Label), die oberhalb der ComboBox angezeigt wird (z. B. "Sprache", "Design", "Auflösung").
        /// </summary>
        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        /// <summary>
        /// Die Auflistung der Einträge für die ComboBox (z. B. Liste von Sprachen oder Themes).
        /// </summary>
        public IEnumerable ItemsSource
        {
            get => (IEnumerable)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        /// <summary>
        /// Der aktuell ausgewählte Eintrag. Standardmäßig Two-Way-Bindung für Datenbindung an das ViewModel.
        /// </summary>
        public object SelectedItem
        {
            get => GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        /// <summary>
        /// Eigenschaftsname des Anzeigetexts pro Eintrag (Standard: "DisplayLabel"). Leer = ToString() der Items.
        /// </summary>
        public string DisplayMemberPath
        {
            get => (string)GetValue(DisplayMemberPathProperty);
            set => SetValue(DisplayMemberPathProperty, value);
        }


        /// <summary>
        /// Event Handler, der Border und TextBlock styled, wenn die InnerComboBox in den Fokus kommt.
        /// </summary>
        private void InnerComboBox_DropDownOpened(object sender, EventArgs e)
        {
            // Border und Label des UserControls stylen
            var borderBrush = Application.Current?.TryFindResource("SettingsControlFocusedBorderBrush") as SolidColorBrush;
            var labelBrush = Application.Current?.TryFindResource("SettingsControlFocusedLabelBrush") as SolidColorBrush;

            if (borderBrush != null) InnerComboBox.BorderBrush = borderBrush;
            if (labelBrush != null) LabelInnerComboBox.Foreground = labelBrush;

            // Border des ComboBox-Templates
            var comboBoxBorder = InnerComboBox.Template?.FindName("Border", InnerComboBox) as Border;
            if(comboBoxBorder != null)
            {
                comboBoxBorder.BorderThickness = new Thickness(1,1,0,0);
                comboBoxBorder.CornerRadius = new CornerRadius(6,0,0,0);
            }

            // Border des ToggleButton-Templates stylen
            var toggleButton = InnerComboBox.Template?.FindName("ToggleButton", InnerComboBox) as ToggleButton;
            var toggleButtonBorder = toggleButton?.Template?.FindName("ToggleBorder", toggleButton) as Border;
            if(toggleButtonBorder != null)
            {
                toggleButtonBorder.BorderThickness = new Thickness(0,1,1,0);
                toggleButtonBorder.CornerRadius = new CornerRadius(0,6,0,0);
            }

        }

        /// <summary>
        /// Event Handler, der Border und TextBlock styled, wenn die InnerComboBox den Fokus verliert.
        /// </summary>
        private void InnerComboBox_DropDownClosed(object sender, EventArgs e)
        {
            // Border und Label des UserControls stylen
            var borderBrush = Application.Current?.TryFindResource("HellgrauBrush") as SolidColorBrush;
            var labelBrush = Application.Current?.TryFindResource("TextSecondaryBrush") as SolidColorBrush;

            if (borderBrush != null) InnerComboBox.BorderBrush = borderBrush;
            if (labelBrush != null) LabelInnerComboBox.Foreground = labelBrush;

            // Border des ComboBox-Templates
            var comboBoxBorder = InnerComboBox.Template?.FindName("Border", InnerComboBox) as Border;
            if(comboBoxBorder != null)
            {
                comboBoxBorder.BorderThickness = new Thickness(1,1,0,1);
                comboBoxBorder.CornerRadius = new CornerRadius(6,0,0,6);
            }

            // Border des ToggleButton-Templates stylen
            var toggleButton = InnerComboBox.Template?.FindName("ToggleButton", InnerComboBox) as ToggleButton;
            var toggleButtonBorder = toggleButton?.Template?.FindName("ToggleBorder", toggleButton) as Border;
            if(toggleButtonBorder != null)
            {
                toggleButtonBorder.BorderThickness = new Thickness(0,1,1,1);
                toggleButtonBorder.CornerRadius = new CornerRadius(0,6,6,0);
            }
        }
    }
}

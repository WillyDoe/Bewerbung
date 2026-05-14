using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Neosmartpen.Net;
using WpfStroke = System.Windows.Ink.Stroke;
using tei_penService_ui.Interfaces;
using tei_penService_ui.Models;
using tei_penService_ui.Services;
using tei_penService_ui.Utilities;
using tei_penService_ui.ViewModels;

namespace tei_penService_ui.Views
{
    /// <summary>
    /// Arbeitsbereich: Desktop-ähnliche Ordner-/Datei-Fläche mit Drag-and-Drop, browserähnliche Notiz-Tabs und ein gemeinsamer <see cref="InkCanvas"/>.
    /// </summary>
    public partial class WorkspaceView : UserControl
    {
        private const double DesktopIconSlotW = 104;
        private const double DesktopIconSlotH = 112;
        private const double DesktopDragThresholdSq = 16.0;

        /// <summary>Strich stammt von der Stift-Pipeline (bereits über den Canvas-Strich abgebildet).</summary>
        private static readonly Guid PenPipelineStrokePropertyId =
            new Guid("a8c3f1b2-7e4d-4c9a-9f10-2d8e6b5c4a01");

        private bool _suppressProgrammaticCanvasStrokes;

        private WorkspaceViewModel _viewModel;
        private readonly WorkspaceStorageService _storage = new WorkspaceStorageService();
        private WorkspaceDesktopLayoutService _layoutService;
        private WorkspaceDesktopLayoutFile _desktopLayout;
        private string _currentDesktopDirectory = string.Empty;
        private ObservableCollection<WorkspaceDocumentTabState> _openTabs;
        private InkCanvasStrokeSink _strokeSink;
        private WorkspaceDocumentTabState _activeTab;
        private readonly ITeiPenServiceWrapper _penServiceWrapper;
        private ObservableCollection<MainViewTabState> _mainViewTabs;
        private MainViewTabState _activeMainViewTab;
        private bool _suspendTabSelectionEvents;
        private bool _suspendMainViewSelectionEvents;
        private Border _selectedDesktopIcon;
        private Border _pendingDragIcon;
        private bool _isDesktopDragActive;
        private Point _pendingDragPointerStart;
        private Point _pendingDragIconOrigin;
        private Border _deleteMenuOwnerIcon;
        private Border _renameMenuOwnerIcon;
        private Point _tabDragStartPoint;
        private bool _tabDragStartPointSet;
        private WorkspaceDocumentTabState _tabDragCandidate;
        private Point _mainTabDragStartPoint;
        private bool _mainTabDragStartPointSet;
        private MainViewTabState _mainTabDragCandidate;
        private CalendarViewMode _activeCalendarViewMode = CalendarViewMode.Week;
        private DateTime _calendarFocusDate = DateTime.Today;
        private bool _calendarUiInitialized;
        private int _calendarYearMinLoaded;
        private int _calendarYearMaxLoaded;
        private bool _isUpdatingCalendarYearList;
        private bool _isCalendarInteractionDragging;
        private Point _calendarInteractionDragStart;
        private double _calendarInteractionPanelStartLeft;
        private double _calendarInteractionPanelStartTop;
        private bool _calendarInteractionSuspendDateRangeSync;
        private bool _isCalendarSidebarVisible;
        private Brush _toolbarCurrentBorderBrush = Brushes.Black;
        private Brush _toolbarCurrentHoverBrush = Brushes.White;
        private const int CalendarYearChunkSize = 20;
        private const int CalendarYearInitialSpan = 20;
        private const double CalendarYearEdgeThreshold = 4.0;

        private static readonly DateTime MockPenCalendarEventDate = new DateTime(2026, 5, 26);
        private static readonly TimeSpan MockPenCalendarDebounce = TimeSpan.FromSeconds(3);
        private const string MockPenCalendarTitle = "Präsentation FH-Potsdam";
        private const string MockPenCalendarName = "Privat";
        private const string MockPenCalendarCategory = "Persönliches Ziel";

        private DispatcherTimer _mockPenCalendarIdleTimer;
        private bool _mockPenCalendarInjectedThisRun;
        private bool _mockPenCalendarShowInMonthGrid;
        private ExitEventHandler _applicationExitHandlerMockPenCalendar;

        /// <summary>Standard-Kategorien für das Ereignis-Interaktionsformular und die Sidebar „Meine Kategorien“.</summary>
        private static readonly string[] DefaultEventCategoryLabels =
        {
            "Hausaufgabe",
            "Prüfung",
            "Persönliches Ziel"
        };
        private enum CalendarViewMode
        {
            Day,
            Week,
            Month
        }

        private sealed class ToolbarColorSet
        {
            public ToolbarColorSet(string borderBrushKey, string hoverBrushKey)
            {
                BorderBrushKey = borderBrushKey;
                HoverBrushKey = hoverBrushKey;
            }

            public string BorderBrushKey { get; }
            public string HoverBrushKey { get; }
        }

        private static readonly ToolbarColorSet[] ToolbarColorCycle =
        {
            new ToolbarColorSet("TeiCyanBrush", "TeiCyanPastelSanftBrush"),
            new ToolbarColorSet("TeiBlueBrush", "TeiBluePastelSanftBrush"),
            new ToolbarColorSet("TeiYellowBrush", "TeiYellowPastelSanftBrush"),
            new ToolbarColorSet("TeiGreenBrush", "TeiGreenPastelSanftBrush")
        };
        private int _toolbarColorCycleIndex;

        /// <summary>True, wenn <see cref="ITeiPenServiceWrapper.DotReceived"/> angebunden ist (echte Stift-Daten).</summary>
        public bool UsesPenServiceWrapper { get; }

        /// <summary>Initialisiert die View ohne Stift-Anbindung (Designer / Fallback).</summary>
        public WorkspaceView()
        {
            InitializeComponent();
            UsesPenServiceWrapper = false;
            _penServiceWrapper = null;
            _viewModel = new WorkspaceViewModel();
            DataContext = _viewModel;
            WireViewModel();
        }

        /// <summary>Initialisiert die View mit Wrapper; das ViewModel abonniert <see cref="ITeiPenServiceWrapper.DotReceived"/>.</summary>
        public WorkspaceView(ITeiPenServiceWrapper penServiceWrapper)
        {
            InitializeComponent();
            UsesPenServiceWrapper = true;
            _penServiceWrapper = penServiceWrapper ?? throw new ArgumentNullException(nameof(penServiceWrapper));
            _viewModel = new WorkspaceViewModel(penServiceWrapper);
            DataContext = _viewModel;
            WireViewModel();
        }

        private void WireViewModel()
        {
            _viewModel.LayoutChanged += ViewModel_LayoutChanged;
            if (_penServiceWrapper != null)
            {
                _penServiceWrapper.RecognizedTextAvailable += PenServiceWrapper_OnRecognizedTextAvailable;
                _penServiceWrapper.DotReceived += OnPenDotReceivedForCalendarMock;
                _penServiceWrapper.ConnectionStatusChanged += PenServiceWrapper_OnConnectionStatusChanged;
            }
        }

        private void PenServiceWrapper_OnConnectionStatusChanged(object sender, bool isConnected)
        {
            try
            {
                if (isConnected)
                    TryRefreshPaperOriginFromCalibrationFiles();
                else
                    _viewModel?.RefreshPaperOriginFromCalibrationFiles(null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PenServiceWrapper_OnConnectionStatusChanged: {ex.Message}");
            }
        }

        /// <summary>Liest Kalibrierungs-JSON (vier Striche) aus dem Datenordner und setzt den NCode-Papier-Ursprung im ViewModel.</summary>
        private void TryRefreshPaperOriginFromCalibrationFiles()
        {
            try
            {
                if (!UsesPenServiceWrapper || _viewModel == null)
                    return;
                var info = _penServiceWrapper?.GetConnectedPenInfo();
                _viewModel.RefreshPaperOriginFromCalibrationFiles(info?.MacAddress);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TryRefreshPaperOriginFromCalibrationFiles: {ex.Message}");
            }
        }

        private void PenServiceWrapper_OnRecognizedTextAvailable(object sender, RecognizedTextEventArgs e)
        {
            if (_activeTab != null && e != null)
                _activeTab.RecognizedTextUtf8 = e.Text ?? string.Empty;
        }

        private void Application_CurrentExit_MockPenCalendar(object sender, ExitEventArgs e)
        {
            try
            {
                CleanupMockPenCalendarAppointment();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Application_CurrentExit_MockPenCalendar: {ex.Message}");
            }
        }

        private bool IsCalendarVisibleInView()
        {
            if (CalendarHost == null || CalendarHost.Visibility != Visibility.Visible)
                return false;
            if (!CalendarHost.IsVisible || !IsVisible)
                return false;
            return true;
        }

        /// <summary>
        /// Bei verbundenem Stift eine neue Workspace-Notiz wie „Datei erstellen“ öffnen, wenn noch kein Dokument-Tab existiert;
        /// nicht auslösen, solange das Kalender-Tab im View sichtbar ist.
        /// </summary>
        private bool EnsureInkDocumentForPenInput()
        {
            try
            {
                if (!UsesPenServiceWrapper || _penServiceWrapper?.GetConnectedPenInfo() == null)
                    return true;
                if (IsCalendarVisibleInView())
                    return true;
                if (_activeTab != null)
                    return true;
                if (_openTabs == null || _storage == null || TabListBox == null)
                    return true;
                if (_openTabs.Count > 0)
                    return true;

                string path = _storage.CreateNewNoteFile(GetTargetFolderForNewNote());
                RefreshDesktopSurface();
                OpenOrActivateTabSync(path);
                SelectBasisMainTab();
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"EnsureInkDocumentForPenInput: {ex.Message}");
                MessageBox.Show($"Datei konnte nicht erstellt werden: {ex.Message}", "Workspace", MessageBoxButton.OK, MessageBoxImage.Warning);
                return true;
            }
        }

        private void EnsureMockPenCalendarIdleTimer()
        {
            if (_mockPenCalendarIdleTimer != null)
                return;

            _mockPenCalendarIdleTimer = new DispatcherTimer { Interval = MockPenCalendarDebounce };
            _mockPenCalendarIdleTimer.Tick += MockPenCalendarIdleTimer_Tick;
        }

        private void CancelMockPenCalendarDebounce()
        {
            _mockPenCalendarIdleTimer?.Stop();
        }

        private void CleanupMockPenCalendarAppointment()
        {
            try
            {
                CancelMockPenCalendarDebounce();
                _mockPenCalendarShowInMonthGrid = false;
                _mockPenCalendarInjectedThisRun = false;

                if (Application.Current != null && _applicationExitHandlerMockPenCalendar != null)
                {
                    Application.Current.Exit -= _applicationExitHandlerMockPenCalendar;
                    _applicationExitHandlerMockPenCalendar = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CleanupMockPenCalendarAppointment: {ex.Message}");
            }
        }

        private void OnPenDotReceivedForCalendarMock(object sender, DotReceivedEventArgs args)
        {
            try
            {
                if (!UsesPenServiceWrapper || _mockPenCalendarInjectedThisRun)
                    return;
                if (!IsCalendarVisibleInView())
                    return;
                if (_penServiceWrapper?.GetConnectedPenInfo() == null)
                {
                    CancelMockPenCalendarDebounce();
                    return;
                }
                if (args?.Dot == null || args.Dot.DotType == DotTypes.PEN_ERROR)
                    return;

                EnsureMockPenCalendarIdleTimer();
                _mockPenCalendarIdleTimer.Stop();
                _mockPenCalendarIdleTimer.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnPenDotReceivedForCalendarMock: {ex.Message}");
            }
        }

        private void MockPenCalendarIdleTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                _mockPenCalendarIdleTimer?.Stop();
                if (_mockPenCalendarInjectedThisRun)
                    return;
                if (!IsCalendarVisibleInView())
                    return;
                if (_penServiceWrapper?.GetConnectedPenInfo() == null)
                    return;

                _mockPenCalendarInjectedThisRun = true;
                _mockPenCalendarShowInMonthGrid = true;
                _calendarFocusDate = MockPenCalendarEventDate;
                _activeCalendarViewMode = CalendarViewMode.Month;
                UpdateCalendarUiState();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MockPenCalendarIdleTimer_Tick: {ex.Message}");
            }
        }

        private void ViewModel_LayoutChanged(object sender, EventArgs e)
        {
            try
            {
                SyncWorkspaceCanvasSize();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ViewModel_LayoutChanged: {ex.Message}");
            }
        }

        private void WorkspaceView_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _storage.EnsureWorkspaceReady();
                _layoutService = new WorkspaceDesktopLayoutService(_storage);
                _desktopLayout = _layoutService.Load();
                _currentDesktopDirectory = Path.GetFullPath(_storage.WorkspaceRootPath);
                _openTabs = new ObservableCollection<WorkspaceDocumentTabState>();
                _mainViewTabs = new ObservableCollection<MainViewTabState>();
                TabListBox.ItemsSource = _openTabs;
                if (MainViewTabListBox != null)
                    MainViewTabListBox.ItemsSource = _mainViewTabs;
                InitializeMainViewTabs();
                InitializeToolbarColorCycle();

                if (_viewModel == null)
                    return;

                _strokeSink = new InkCanvasStrokeSink(WorkspaceInkCanvas);
                _viewModel.StrokeSink = _strokeSink;
                if (UsesPenServiceWrapper)
                    _viewModel.EnsureInkDocumentForPenInput = EnsureInkDocumentForPenInput;
                WorkspaceInkCanvas.StrokeCollected += WorkspaceInkCanvas_StrokeCollected;
                _viewModel.SetDisplayOffset(0f, 0f);
                UpdateSessionActiveFlag();
                RefreshDesktopSurface();
                TryRefreshPaperOriginFromCalibrationFiles();
                SyncWorkspaceCanvasSize();

                if (Application.Current != null)
                {
                    _applicationExitHandlerMockPenCalendar = Application_CurrentExit_MockPenCalendar;
                    Application.Current.Exit -= _applicationExitHandlerMockPenCalendar;
                    Application.Current.Exit += _applicationExitHandlerMockPenCalendar;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WorkspaceView_Loaded: {ex.Message}");
            }
        }

        private void WorkspaceView_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                PersistDesktopLayout();
                CleanupMockPenCalendarAppointment();
                if (_viewModel != null)
                {
                    _viewModel.EnsureInkDocumentForPenInput = null;
                    _viewModel.LayoutChanged -= ViewModel_LayoutChanged;
                }
                if (_penServiceWrapper != null)
                {
                    _penServiceWrapper.ConnectionStatusChanged -= PenServiceWrapper_OnConnectionStatusChanged;
                    _penServiceWrapper.RecognizedTextAvailable -= PenServiceWrapper_OnRecognizedTextAvailable;
                    _penServiceWrapper.DotReceived -= OnPenDotReceivedForCalendarMock;
                }
                WorkspaceInkCanvas.StrokeCollected -= WorkspaceInkCanvas_StrokeCollected;
                _viewModel?.Unsubscribe();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WorkspaceView_Unloaded: {ex.Message}");
            }
        }

        private void WorkspaceInkCanvas_StrokeCollected(object sender, InkCanvasStrokeCollectedEventArgs e)
        {
            try
            {
                if (_suppressProgrammaticCanvasStrokes || _viewModel == null)
                    return;

                WpfStroke stroke = e?.Stroke;
                if (stroke == null)
                    return;
                if (stroke.ContainsPropertyData(PenPipelineStrokePropertyId))
                    return;

                if (_viewModel.IsInkSessionActive)
                    _viewModel.NotifyUserInkStrokeCollected();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WorkspaceInkCanvas_StrokeCollected: {ex.Message}");
            }
        }

        private void WorkspaceView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            try
            {
                SyncWorkspaceCanvasSize();
                ResizeDesktopCanvasExtent();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WorkspaceView_SizeChanged: {ex.Message}");
            }
        }

        /// <summary>
        /// Logisches InkCanvas = genau eine A5-Fläche in DIP; der Viewbox WorkspaceA5Viewbox skaliert mit Stretch=Fill
        /// auf den Tab (mit möglicher Verzerrung). Kein Scrollen, keine Vergrößerung durch Striche.
        /// </summary>
        private void SyncWorkspaceCanvasSize()
        {
            if (_viewModel == null)
                return;
            var scroll = WorkspaceScroll;
            double viewportW = scroll?.ActualWidth ?? ActualWidth;
            double viewportH = scroll?.ActualHeight ?? ActualHeight;
            if (viewportW <= 0)
                return;

            double needW = _viewModel.GetRequiredCanvasWidth();
            double needH = _viewModel.GetRequiredCanvasHeight();

            if (needW > 0)
                WorkspaceInkCanvas.Width = needW;
            if (needH > 0)
                WorkspaceInkCanvas.Height = needH;

            if (viewportW > 0 && viewportH > 0 && FindName("WorkspaceA5PageHost") is Border pageHost)
            {
                pageHost.Width = viewportW;
                pageHost.Height = viewportH;
            }
        }

        private void PersistDesktopLayout()
        {
            try
            {
                if (_layoutService != null && _desktopLayout != null)
                    _layoutService.Save(_desktopLayout);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PersistDesktopLayout: {ex.Message}");
            }
        }

        /// <summary>Zielordner für neue Notizen: aktuell geöffnete Desktop-Ebene.</summary>
        private string GetTargetFolderForNewNote()
        {
            if (!string.IsNullOrEmpty(_currentDesktopDirectory) && Directory.Exists(_currentDesktopDirectory))
                return _currentDesktopDirectory;
            return _storage.GetDefaultTargetFolderPath();
        }

        private async void CreateFileButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string targetFolder = GetTargetFolderForNewNote();
                string path = await _storage.CreateNewNoteFileAsync(targetFolder).ConfigureAwait(true);
                RefreshDesktopSurface();
                await OpenOrActivateTabAsync(path).ConfigureAwait(true);
                SelectBasisMainTab();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CreateFileButton_Click: {ex.Message}");
                MessageBox.Show($"Datei konnte nicht erstellt werden: {ex.Message}", "Workspace", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SelectBasisMainTab()
        {
            if (_mainViewTabs == null)
                return;

            var basisTab = _mainViewTabs.FirstOrDefault(t =>
                string.Equals(t.Key, "basis", StringComparison.OrdinalIgnoreCase));
            SelectMainTab(basisTab);
        }

        private void CreateFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string parentDirectory = GetTargetFolderForNewNote();
                string folderName = BuildNextNewFolderName(parentDirectory);
                _storage.CreateChildFolder(parentDirectory, folderName);
                RefreshDesktopSurface();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CreateFolderButton_Click: {ex.Message}");
                MessageBox.Show($"Ordner konnte nicht erstellt werden: {ex.Message}", "Workspace", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void WorkspaceToolbarButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var button = sender as Button;
                if (button == null || button.Template == null)
                    return;
                ApplyNextToolbarColor(button);

                var rippleElement = button.Template.FindName("RippleEllipse", button) as FrameworkElement;
                if (rippleElement == null)
                    return;

                var rippleScaleTransform = button.Template.FindName("RippleScaleTransform", button) as ScaleTransform;
                if (rippleScaleTransform == null)
                    return;

                var rippleTranslateTransform = button.Template.FindName("RippleTranslateTransform", button) as TranslateTransform;
                if (rippleTranslateTransform == null)
                    return;

                Point clickPoint = e.GetPosition(button);
                double rippleDiameter = Math.Max(button.ActualWidth, button.ActualHeight);
                if (rippleDiameter <= 0)
                    return;

                rippleElement.Width = rippleDiameter;
                rippleElement.Height = rippleDiameter;
                rippleTranslateTransform.X = clickPoint.X - (rippleDiameter / 2.0);
                rippleTranslateTransform.Y = clickPoint.Y - (rippleDiameter / 2.0);
                rippleScaleTransform.ScaleX = 0;
                rippleScaleTransform.ScaleY = 0;
                rippleElement.Opacity = 0.35;

                var rippleDuration = TimeSpan.FromMilliseconds(350);
                var opacityAnimation = new DoubleAnimation(0.35, 0, rippleDuration);
                var scaleXAnimation = new DoubleAnimation(0, 1.8, rippleDuration);
                var scaleYAnimation = new DoubleAnimation(0, 1.8, rippleDuration);

                rippleElement.BeginAnimation(OpacityProperty, opacityAnimation);
                rippleScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnimation);
                rippleScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnimation);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WorkspaceToolbarButton_PreviewMouseLeftButtonDown: {ex.Message}");
            }
        }

        private void WorkspaceToolbarButton_MouseEnter(object sender, MouseEventArgs e)
        {
            try
            {
                var button = sender as Button;
                if (button == null)
                    return;
                button.Background = _toolbarCurrentHoverBrush;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WorkspaceToolbarButton_MouseEnter: {ex.Message}");
            }
        }

        private void WorkspaceToolbarButton_MouseLeave(object sender, MouseEventArgs e)
        {
            try
            {
                var button = sender as Button;
                if (button == null)
                    return;
                button.Background = Brushes.White;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WorkspaceToolbarButton_MouseLeave: {ex.Message}");
            }
        }

        private void InitializeToolbarColorCycle()
        {
            try
            {
                _toolbarColorCycleIndex = 0;
                ApplyToolbarColor(ToolbarColorCycle[_toolbarColorCycleIndex]);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"InitializeToolbarColorCycle: {ex.Message}");
            }
        }

        private void ApplyNextToolbarColor(Button button)
        {
            if (button == null)
                return;

            _toolbarColorCycleIndex = (_toolbarColorCycleIndex + 1) % ToolbarColorCycle.Length;
            ApplyToolbarColor(ToolbarColorCycle[_toolbarColorCycleIndex]);
        }

        private void ApplyToolbarColor(ToolbarColorSet colorSet)
        {
            if (colorSet == null)
                return;

            if (TryFindResource(colorSet.BorderBrushKey) is Brush borderBrush)
                _toolbarCurrentBorderBrush = borderBrush;
            if (TryFindResource(colorSet.HoverBrushKey) is Brush hoverBrush)
                _toolbarCurrentHoverBrush = hoverBrush;

            ApplyToolbarColorToAllButtons();
        }

        private void ApplyToolbarColorToAllButtons()
        {
            foreach (var button in GetToolbarButtons())
            {
                if (button == null)
                    continue;

                button.BorderBrush = _toolbarCurrentBorderBrush;
                button.Background = button.IsMouseOver ? _toolbarCurrentHoverBrush : Brushes.White;
            }
        }

        private IEnumerable<Button> GetToolbarButtons()
        {
            yield return CreateScratchpadCell;
            yield return CreateFileCell;
            yield return CreateFolderCell;
            yield return CameraCell;
            yield return MicrophoneCell;
        }

        private void InitializeMainViewTabs()
        {
            try
            {
                if (_mainViewTabs == null)
                    return;

                _mainViewTabs.Clear();
                var basisTab = new MainViewTabState("basis", "Basis", canClose: false);
                _mainViewTabs.Add(basisTab);
                _activeMainViewTab = null;

                _suspendMainViewSelectionEvents = true;
                if (MainViewTabListBox != null)
                    MainViewTabListBox.SelectedItem = basisTab;
                _suspendMainViewSelectionEvents = false;

                ApplyMainViewSelection(basisTab);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"InitializeMainViewTabs: {ex.Message}");
            }
        }

        private void MainViewTabListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (_suspendMainViewSelectionEvents)
                    return;

                var next = MainViewTabListBox?.SelectedItem as MainViewTabState;
                ApplyMainViewSelection(next);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MainViewTabListBox_OnSelectionChanged: {ex.Message}");
            }
        }

        private void ApplyMainViewSelection(MainViewTabState next)
        {
            if (next == null || next == _activeMainViewTab)
                return;

            _activeMainViewTab = next;
            bool isCalendar = string.Equals(next.Key, "calendar", StringComparison.OrdinalIgnoreCase);
            if (BasisHost != null)
                BasisHost.Visibility = isCalendar ? Visibility.Collapsed : Visibility.Visible;
            // EditorChrome bleibt im Basis-Tab zustandsbasiert (abhängig von offenen Dokumenttabs).
            EditorChrome.Visibility = isCalendar ? Visibility.Collapsed : EditorChrome.Visibility;
            if (CalendarHost != null)
                CalendarHost.Visibility = isCalendar ? Visibility.Visible : Visibility.Collapsed;
            if (isCalendar)
                EnsureCalendarUiInitialized();
            else
            {
                var yearDropdownPanel = FindCalendarElement<Popup>("CalendarYearDropdownPanel");
                if (yearDropdownPanel != null)
                    yearDropdownPanel.IsOpen = false;
                SetCalendarSidebarVisible(false);
                SetCalendarInteractionVisible(false);
            }
            UpdateCalendarButtonIcon(isCalendar);
            if (!isCalendar)
                CancelMockPenCalendarDebounce();
        }

        private void UpdateCalendarButtonIcon(bool isCalendarTabActive)
        {
            if (CalendarButton != null)
                CalendarButton.ToolTip = isCalendarTabActive ? "Ereignis hinzufügen" : "Kalender";

            if (!(FindName("CalendarButtonIcon") is TextBlock calendarButtonIcon))
                return;

            if (isCalendarTabActive)
            {
                calendarButtonIcon.FontFamily = new FontFamily("Segoe UI");
                calendarButtonIcon.Text = "+";
                return;
            }

            calendarButtonIcon.FontFamily = new FontFamily("Segoe MDL2 Assets");
            calendarButtonIcon.Text = "\uE787";
        }

        private void CalendarButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool isCalendarTabActive = _activeMainViewTab != null &&
                                           string.Equals(_activeMainViewTab.Key, "calendar", StringComparison.OrdinalIgnoreCase);

                if (isCalendarTabActive)
                {
                    SetCalendarInteractionVisible(true);
                    return;
                }

                OpenCalendarMainTab();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CalendarButton_Click: {ex.Message}");
            }
        }

        private void CalendarInteractionCloseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SetCalendarInteractionVisible(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CalendarInteractionCloseButton_Click: {ex.Message}");
            }
        }

        private void SetCalendarInteractionVisible(bool isVisible)
        {
            if (CalendarButton != null)
                CalendarButton.Visibility = isVisible ? Visibility.Collapsed : Visibility.Visible;

            var interactionPanel = FindCalendarElement<Border>("CalendarInteractionPanel");
            if (interactionPanel != null)
            {
                interactionPanel.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
                if (isVisible)
                {
                    if (double.IsNaN(Canvas.GetLeft(interactionPanel)))
                        Canvas.SetLeft(interactionPanel, 80);
                    if (double.IsNaN(Canvas.GetTop(interactionPanel)))
                        Canvas.SetTop(interactionPanel, 50);
                }
            }

            if (isVisible)
            {
                var yearDropdownPanel = FindCalendarElement<Popup>("CalendarYearDropdownPanel");
                if (yearDropdownPanel != null)
                    yearDropdownPanel.IsOpen = false;

                InitializeCalendarInteractionForm();
            }
            else
            {
                _isCalendarInteractionDragging = false;
                if (Mouse.Captured is UIElement captured)
                    captured.ReleaseMouseCapture();
            }
        }

        private void InitializeCalendarInteractionForm()
        {
            try
            {
                var calendarComboBox = FindCalendarElement<ComboBox>("CalendarInteractionCalendarComboBox");
                if (calendarComboBox != null && calendarComboBox.Items.Count == 0)
                {
                    calendarComboBox.Items.Add("Schule");
                    calendarComboBox.Items.Add("Privat");
                }

                var typeComboBox = FindCalendarElement<ComboBox>("CalendarInteractionTypeComboBox");
                if (typeComboBox != null && typeComboBox.Items.Count == 0)
                {
                    foreach (string label in DefaultEventCategoryLabels)
                        typeComboBox.Items.Add(label);
                }

                var hourComboBox = FindCalendarElement<ComboBox>("CalendarInteractionHourComboBox");
                if (hourComboBox != null && hourComboBox.Items.Count == 0)
                {
                    foreach (string hour in Enumerable.Range(0, 24).Select(value => value.ToString("D2")))
                    {
                        hourComboBox.Items.Add(hour);
                    }
                }

                var minuteComboBox = FindCalendarElement<ComboBox>("CalendarInteractionMinuteComboBox");
                if (minuteComboBox != null && minuteComboBox.Items.Count == 0)
                {
                    foreach (string minute in Enumerable.Range(0, 60).Select(value => value.ToString("D2")))
                    {
                        minuteComboBox.Items.Add(minute);
                    }
                }

                var endHourComboBox = FindCalendarElement<ComboBox>("CalendarInteractionEndHourComboBox");
                if (endHourComboBox != null && endHourComboBox.Items.Count == 0)
                {
                    foreach (string hour in Enumerable.Range(0, 24).Select(value => value.ToString("D2")))
                    {
                        endHourComboBox.Items.Add(hour);
                    }
                }

                var endMinuteComboBox = FindCalendarElement<ComboBox>("CalendarInteractionEndMinuteComboBox");
                if (endMinuteComboBox != null && endMinuteComboBox.Items.Count == 0)
                {
                    foreach (string minute in Enumerable.Range(0, 60).Select(value => value.ToString("D2")))
                    {
                        endMinuteComboBox.Items.Add(minute);
                    }
                }

                _calendarInteractionSuspendDateRangeSync = true;
                try
                {
                    var datePicker = FindCalendarElement<DatePicker>("CalendarInteractionDatePicker");
                    var endDatePicker = FindCalendarElement<DatePicker>("CalendarInteractionEndDatePicker");
                    var today = DateTime.Today;
                    if (datePicker != null)
                        datePicker.SelectedDate = today;
                    if (endDatePicker != null)
                        endDatePicker.SelectedDate = today;
                }
                finally
                {
                    _calendarInteractionSuspendDateRangeSync = false;
                }

                if (calendarComboBox != null && calendarComboBox.SelectedIndex < 0 && calendarComboBox.Items.Count > 0)
                    calendarComboBox.SelectedIndex = 0;

                if (typeComboBox != null && typeComboBox.SelectedIndex < 0 && typeComboBox.Items.Count > 0)
                    typeComboBox.SelectedIndex = 0;

                DateTime now = DateTime.Now;
                if (hourComboBox != null)
                    hourComboBox.SelectedItem = now.Hour.ToString("D2");
                if (minuteComboBox != null)
                    minuteComboBox.SelectedItem = now.Minute.ToString("D2");

                var endTime = now.AddHours(1);
                if (endHourComboBox != null)
                    endHourComboBox.SelectedItem = endTime.Hour.ToString("D2");
                if (endMinuteComboBox != null)
                    endMinuteComboBox.SelectedItem = endTime.Minute.ToString("D2");

                var allDayCheckBox = FindCalendarElement<CheckBox>("CalendarInteractionAllDayCheckBox");
                if (allDayCheckBox != null)
                    allDayCheckBox.IsChecked = false;

                UpdateCalendarInteractionTimeSelectionState();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"InitializeCalendarInteractionForm: {ex.Message}");
            }
        }

        private void UpdateCalendarInteractionTimeSelectionState()
        {
            try
            {
                var startDatePicker = FindCalendarElement<DatePicker>("CalendarInteractionDatePicker");
                var endDatePicker = FindCalendarElement<DatePicker>("CalendarInteractionEndDatePicker");
                var allDayCheckBox = FindCalendarElement<CheckBox>("CalendarInteractionAllDayCheckBox");

                DateTime? startDate = startDatePicker?.SelectedDate;
                DateTime? endDate = endDatePicker?.SelectedDate;
                bool isMultiDay = startDate.HasValue && endDate.HasValue
                    && endDate.Value.Date > startDate.Value.Date;

                if (isMultiDay && allDayCheckBox != null)
                {
                    allDayCheckBox.IsEnabled = false;
                    if (allDayCheckBox.IsChecked == true)
                    {
                        _calendarInteractionSuspendDateRangeSync = true;
                        try
                        {
                            allDayCheckBox.IsChecked = false;
                        }
                        finally
                        {
                            _calendarInteractionSuspendDateRangeSync = false;
                        }
                    }
                }
                else if (allDayCheckBox != null)
                {
                    allDayCheckBox.IsEnabled = true;
                }

                bool isAllDay = allDayCheckBox?.IsChecked == true;
                bool timeEnabled = !isAllDay;

                var hourComboBox = FindCalendarElement<ComboBox>("CalendarInteractionHourComboBox");
                if (hourComboBox != null)
                    hourComboBox.IsEnabled = timeEnabled;

                var minuteComboBox = FindCalendarElement<ComboBox>("CalendarInteractionMinuteComboBox");
                if (minuteComboBox != null)
                    minuteComboBox.IsEnabled = timeEnabled;

                var endHourComboBox = FindCalendarElement<ComboBox>("CalendarInteractionEndHourComboBox");
                if (endHourComboBox != null)
                    endHourComboBox.IsEnabled = timeEnabled;

                var endMinuteComboBox = FindCalendarElement<ComboBox>("CalendarInteractionEndMinuteComboBox");
                if (endMinuteComboBox != null)
                    endMinuteComboBox.IsEnabled = timeEnabled;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateCalendarInteractionTimeSelectionState: {ex.Message}");
            }
        }

        private void CalendarInteractionDateRange_Changed(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (_calendarInteractionSuspendDateRangeSync)
                    return;

                var startDatePicker = FindCalendarElement<DatePicker>("CalendarInteractionDatePicker");
                var endDatePicker = FindCalendarElement<DatePicker>("CalendarInteractionEndDatePicker");
                if (startDatePicker == null || endDatePicker == null)
                    return;

                if (!startDatePicker.SelectedDate.HasValue || !endDatePicker.SelectedDate.HasValue)
                {
                    UpdateCalendarInteractionTimeSelectionState();
                    return;
                }

                if (endDatePicker.SelectedDate.Value.Date < startDatePicker.SelectedDate.Value.Date)
                {
                    _calendarInteractionSuspendDateRangeSync = true;
                    try
                    {
                        if (ReferenceEquals(sender, endDatePicker))
                            endDatePicker.SelectedDate = startDatePicker.SelectedDate;
                        else
                            startDatePicker.SelectedDate = endDatePicker.SelectedDate;
                    }
                    finally
                    {
                        _calendarInteractionSuspendDateRangeSync = false;
                    }
                }

                UpdateCalendarInteractionTimeSelectionState();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CalendarInteractionDateRange_Changed: {ex.Message}");
            }
        }

        private void CalendarInteractionAllDayCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateCalendarInteractionTimeSelectionState();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CalendarInteractionAllDayCheckBox_Checked: {ex.Message}");
            }
        }

        private void CalendarInteractionLinkGoalButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MessageBox.Show(
                    "Die Verknüpfung zu einem Projekt wird in einem nächsten Schritt implementiert.",
                    "Projekt verknüpfen",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CalendarInteractionLinkGoalButton_Click: {ex.Message}");
            }
        }

        private void CalendarInteractionLinkEventButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MessageBox.Show(
                    "Die Verknüpfung zu einem Ereignis wird in einem nächsten Schritt implementiert.",
                    "Ereignis verknüpfen",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CalendarInteractionLinkEventButton_Click: {ex.Message}");
            }
        }

        private void CalendarInteractionPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.ChangedButton != MouseButton.Left)
                    return;
                if (!(sender is UIElement header))
                    return;
                if (e.OriginalSource is DependencyObject os && CalendarInteractionEventHeaderHitButton(os, header))
                    return;

                var panel = FindCalendarElement<Border>("CalendarInteractionPanel");
                if (panel == null || CalendarHost == null)
                    return;

                _isCalendarInteractionDragging = true;
                _calendarInteractionDragStart = e.GetPosition(CalendarHost);
                _calendarInteractionPanelStartLeft = Canvas.GetLeft(panel);
                _calendarInteractionPanelStartTop = Canvas.GetTop(panel);
                if (double.IsNaN(_calendarInteractionPanelStartLeft))
                    _calendarInteractionPanelStartLeft = 80;
                if (double.IsNaN(_calendarInteractionPanelStartTop))
                    _calendarInteractionPanelStartTop = 50;

                // Capture muss am gleichen Element erfolgen, das MouseMove/MouseLeftButtonUp abonniert (WPF-Headerrand).
                header.CaptureMouse();
                e.Handled = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CalendarInteractionPanel_MouseLeftButtonDown: {ex.Message}");
            }
        }

        private void CalendarInteractionPanel_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                var panel = FindCalendarElement<Border>("CalendarInteractionPanel");
                if (!_isCalendarInteractionDragging || panel == null || CalendarHost == null)
                    return;

                Point current = e.GetPosition(CalendarHost);
                double deltaX = current.X - _calendarInteractionDragStart.X;
                double deltaY = current.Y - _calendarInteractionDragStart.Y;

                double targetLeft = _calendarInteractionPanelStartLeft + deltaX;
                double targetTop = _calendarInteractionPanelStartTop + deltaY;

                double maxLeft = Math.Max(0, CalendarHost.ActualWidth - panel.ActualWidth);
                double maxTop = Math.Max(0, CalendarHost.ActualHeight - panel.ActualHeight);
                double clampedLeft = Math.Max(0, Math.Min(targetLeft, maxLeft));
                double clampedTop = Math.Max(0, Math.Min(targetTop, maxTop));

                Canvas.SetLeft(panel, clampedLeft);
                Canvas.SetTop(panel, clampedTop);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CalendarInteractionPanel_MouseMove: {ex.Message}");
            }
        }

        private void CalendarInteractionPanel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.ChangedButton != MouseButton.Left)
                    return;
                if (!(sender is UIElement header))
                    return;

                _isCalendarInteractionDragging = false;
                if (ReferenceEquals(Mouse.Captured, header))
                    header.ReleaseMouseCapture();
                e.Handled = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CalendarInteractionPanel_MouseLeftButtonUp: {ex.Message}");
            }
        }

        private void CalendarInteractionPanelDragHeader_LostMouseCapture(object sender, MouseEventArgs e)
        {
            _isCalendarInteractionDragging = false;
        }

        private static bool CalendarInteractionEventHeaderHitButton(DependencyObject source, object header)
        {
            for (var cur = source; cur != null; cur = VisualTreeHelper.GetParent(cur))
            {
                if (cur is System.Windows.Controls.Button)
                    return true;
                if (ReferenceEquals(cur, header))
                    break;
            }

            return false;
        }

        private void EnsureCalendarUiInitialized()
        {
            if (_calendarUiInitialized)
                return;

            PopulateCalendarYearList();
            var miniCalendar = FindCalendarElement<System.Windows.Controls.Calendar>("CalendarSidebarMiniCalendar");
            if (miniCalendar != null)
            {
                miniCalendar.SelectedDate = DateTime.Today;
                miniCalendar.DisplayDate = DateTime.Today;
            }
            _calendarUiInitialized = true;
            UpdateCalendarUiState();
        }

        private void UpdateCalendarUiState()
        {
            var monthText = FindCalendarElement<TextBlock>("CalendarMonthText");
            var yearText = FindCalendarElement<TextBlock>("CalendarYearText");
            if (monthText != null)
                monthText.Text = _calendarFocusDate.ToString("MMMM");
            if (yearText != null)
                yearText.Text = _calendarFocusDate.Year.ToString();

            UpdateCalendarModeButtonState(FindCalendarElement<Button>("CalendarDayModeButton"), _activeCalendarViewMode == CalendarViewMode.Day);
            UpdateCalendarModeButtonState(FindCalendarElement<Button>("CalendarWeekModeButton"), _activeCalendarViewMode == CalendarViewMode.Week);
            UpdateCalendarModeButtonState(FindCalendarElement<Button>("CalendarMonthModeButton"), _activeCalendarViewMode == CalendarViewMode.Month);

            var dayViewHost = FindCalendarElement<Grid>("DayViewHost");
            var weekViewHost = FindCalendarElement<Grid>("WeekViewHost");
            var monthViewHost = FindCalendarElement<Grid>("MonthViewHost");
            if (dayViewHost != null)
                dayViewHost.Visibility = _activeCalendarViewMode == CalendarViewMode.Day ? Visibility.Visible : Visibility.Collapsed;
            if (weekViewHost != null)
                weekViewHost.Visibility = _activeCalendarViewMode == CalendarViewMode.Week ? Visibility.Visible : Visibility.Collapsed;
            if (monthViewHost != null)
                monthViewHost.Visibility = _activeCalendarViewMode == CalendarViewMode.Month ? Visibility.Visible : Visibility.Collapsed;

            PopulateCalendarMonthDayCells();
        }

        /// <summary>
        /// Baut die Monatsraster-Zellen (6×7) ausgehend von <see cref="_calendarFocusDate"/>:
        /// kompakter Kopf mit Tageszahl und Wochentag, darunter freier Bereich für Termine.
        /// </summary>
        private void PopulateCalendarMonthDayCells()
        {
            try
            {
                var host = FindCalendarElement<UniformGrid>("CalendarMonthDayCellsHost");
                if (host == null)
                    return;

                host.Children.Clear();

                var app = System.Windows.Application.Current;
                var borderBrush = app?.TryFindResource("BorderBrush") as Brush
                    ?? SystemColors.ControlDarkBrush;
                var bgWeekday = app?.TryFindResource("BackgroundBrush") as Brush ?? Brushes.White;
                var bgWeekend = app?.TryFindResource("TeiYellowPastelSanftBrush") as Brush ?? bgWeekday;
                var fgPrimary = app?.TryFindResource("TextBrush") as Brush ?? Brushes.Black;
                var fgSecondary = app?.TryFindResource("TextSecondaryBrush") as Brush
                    ?? new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66));
                var fgMuted = app?.TryFindResource("TextSecondaryBrush") as Brush
                    ?? new SolidColorBrush(Color.FromArgb(0x88, 0x33, 0x33, 0x33));
                var todayAccent = app?.TryFindResource("TeiCyanBrush") as Brush
                    ?? new SolidColorBrush(Color.FromRgb(0, 0x82, 0xCD));
                var primaryFont = app?.TryFindResource("PrimaryFontFamily") as FontFamily;

                var culture = CultureInfo.CurrentCulture;
                int year = _calendarFocusDate.Year;
                int month = _calendarFocusDate.Month;
                var firstOfMonth = new DateTime(year, month, 1);
                int leading = ((int)firstOfMonth.DayOfWeek + 6) % 7;

                var today = DateTime.Today;

                for (int i = 0; i < 42; i++)
                {
                    var cellDate = firstOfMonth.AddDays(i - leading);
                    bool inMonth = cellDate.Month == month;
                    bool isToday = cellDate.Date == today.Date;
                    bool weekend = cellDate.DayOfWeek == DayOfWeek.Saturday || cellDate.DayOfWeek == DayOfWeek.Sunday;

                    var shell = new Border
                    {
                        BorderBrush = borderBrush,
                        BorderThickness = new Thickness(1),
                        Background = weekend ? bgWeekend : bgWeekday,
                        Tag = cellDate,
                        SnapsToDevicePixels = true
                    };

                    if (isToday)
                    {
                        shell.BorderBrush = todayAccent;
                        shell.BorderThickness = new Thickness(2);
                    }

                    var root = new Grid();
                    root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                    string weekdayShort = cellDate.ToString("ddd", culture).TrimEnd('.');

                    var headerStack = new StackPanel { Margin = new Thickness(6, 4, 4, 2) };
                    var dayNumber = new TextBlock
                    {
                        Text = cellDate.Day.ToString(culture),
                        FontSize = 13,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = inMonth ? fgPrimary : fgMuted
                    };
                    var weekdayAbbr = new TextBlock
                    {
                        Text = weekdayShort,
                        FontSize = 10,
                        Margin = new Thickness(0, 1, 0, 0),
                        Foreground = inMonth ? fgSecondary : fgMuted
                    };
                    if (primaryFont != null)
                    {
                        dayNumber.FontFamily = primaryFont;
                        weekdayAbbr.FontFamily = primaryFont;
                    }

                    headerStack.Children.Add(dayNumber);
                    headerStack.Children.Add(weekdayAbbr);

                    Grid.SetRow(headerStack, 0);
                    root.Children.Add(headerStack);

                    var eventSlot = new Border
                    {
                        Background = Brushes.Transparent,
                        Margin = new Thickness(2, 0, 2, 2)
                    };

                    if (_mockPenCalendarShowInMonthGrid && cellDate.Date == MockPenCalendarEventDate.Date)
                    {
                        var chipBg = app?.TryFindResource("TeiCyanPastelSanftBrush") as Brush
                            ?? new SolidColorBrush(Color.FromRgb(0xD4, 0xF4, 0xF7));
                        var chipFg = app?.TryFindResource("TextBrush") as Brush ?? Brushes.Black;
                        var chipBorder = app?.TryFindResource("TeiCyanBrush") as Brush
                            ?? new SolidColorBrush(Color.FromRgb(0, 0x82, 0xCD));

                        var chip = new Border
                        {
                            Background = chipBg,
                            BorderBrush = chipBorder,
                            BorderThickness = new Thickness(1),
                            CornerRadius = new CornerRadius(4),
                            Padding = new Thickness(4, 2, 4, 2),
                            Margin = new Thickness(0, 2, 0, 0)
                        };
                        var stack = new StackPanel();
                        stack.Children.Add(new TextBlock
                        {
                            Text = MockPenCalendarTitle,
                            FontSize = 11,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = chipFg,
                            TextTrimming = TextTrimming.CharacterEllipsis
                        });
                        stack.Children.Add(new TextBlock
                        {
                            Text = "15:00–16:00",
                            FontSize = 10,
                            Foreground = fgSecondary,
                            Margin = new Thickness(0, 2, 0, 0)
                        });
                        stack.Children.Add(new TextBlock
                        {
                            Text = MockPenCalendarName + " · " + MockPenCalendarCategory,
                            FontSize = 9,
                            Foreground = fgSecondary,
                            Margin = new Thickness(0, 1, 0, 0),
                            TextTrimming = TextTrimming.CharacterEllipsis
                        });
                        chip.Child = stack;
                        eventSlot.Child = chip;
                    }

                    Grid.SetRow(eventSlot, 1);
                    root.Children.Add(eventSlot);

                    shell.Child = root;
                    host.Children.Add(shell);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PopulateCalendarMonthDayCells: {ex.Message}");
            }
        }

        private static void UpdateCalendarModeButtonState(Button button, bool isActive)
        {
            if (button == null)
                return;

            var app = System.Windows.Application.Current;
            var activeBackground = app?.TryFindResource("TeiCyanBrush") as Brush
                ?? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 0x82, 0xCD));
            var activeForeground = app?.TryFindResource("WeißBrush") as Brush ?? Brushes.White;
            var inactiveForeground = app?.TryFindResource("TextBrush") as Brush ?? Brushes.Black;

            if (isActive)
            {
                button.Background = activeBackground;
                button.Foreground = activeForeground;
            }
            else
            {
                button.Background = Brushes.Transparent;
                button.Foreground = inactiveForeground;
            }
        }

        private void PopulateCalendarYearList()
        {
            var yearListBox = FindCalendarElement<ListBox>("CalendarYearListBox");
            if (yearListBox == null)
                return;

            _isUpdatingCalendarYearList = true;
            try
            {
                if (yearListBox.Items.Count == 0)
                {
                    int centerYear = _calendarFocusDate.Year;
                    _calendarYearMinLoaded = Math.Max(DateTime.MinValue.Year, centerYear - CalendarYearInitialSpan);
                    _calendarYearMaxLoaded = Math.Min(DateTime.MaxValue.Year, centerYear + CalendarYearInitialSpan);

                    yearListBox.Items.Clear();
                    for (int year = _calendarYearMinLoaded; year <= _calendarYearMaxLoaded; year++)
                    {
                        yearListBox.Items.Add(year);
                    }
                }

                EnsureCalendarYearInLoadedRange(yearListBox, _calendarFocusDate.Year);
                yearListBox.SelectedItem = _calendarFocusDate.Year;
                yearListBox.ScrollIntoView(_calendarFocusDate.Year);
            }
            finally
            {
                _isUpdatingCalendarYearList = false;
            }
        }

        private void EnsureCalendarYearInLoadedRange(ListBox yearListBox, int targetYear)
        {
            if (yearListBox == null)
                return;

            while (targetYear < _calendarYearMinLoaded && _calendarYearMinLoaded > DateTime.MinValue.Year)
            {
                AppendCalendarYearsTop(yearListBox, CalendarYearChunkSize);
            }

            while (targetYear > _calendarYearMaxLoaded && _calendarYearMaxLoaded < DateTime.MaxValue.Year)
            {
                AppendCalendarYearsBottom(yearListBox, CalendarYearChunkSize);
            }
        }

        private int AppendCalendarYearsTop(ListBox yearListBox, int count)
        {
            if (yearListBox == null || _calendarYearMinLoaded <= DateTime.MinValue.Year || count <= 0)
                return 0;

            int oldMin = _calendarYearMinLoaded;
            int newMin = Math.Max(DateTime.MinValue.Year, oldMin - count);
            int addedCount = oldMin - newMin;
            if (addedCount <= 0)
                return 0;

            for (int year = oldMin - 1; year >= newMin; year--)
            {
                yearListBox.Items.Insert(0, year);
            }

            _calendarYearMinLoaded = newMin;
            return addedCount;
        }

        private int AppendCalendarYearsBottom(ListBox yearListBox, int count)
        {
            if (yearListBox == null || _calendarYearMaxLoaded >= DateTime.MaxValue.Year || count <= 0)
                return 0;

            int oldMax = _calendarYearMaxLoaded;
            int newMax = Math.Min(DateTime.MaxValue.Year, oldMax + count);
            if (newMax <= oldMax)
                return 0;

            for (int year = oldMax + 1; year <= newMax; year++)
            {
                yearListBox.Items.Add(year);
            }

            _calendarYearMaxLoaded = newMax;
            return newMax - oldMax;
        }

        private void ToggleCalendarYearDropdown()
        {
            var yearDropdownPanel = FindCalendarElement<Popup>("CalendarYearDropdownPanel");
            if (yearDropdownPanel == null)
                return;

            yearDropdownPanel.IsOpen = !yearDropdownPanel.IsOpen;
        }

        private T FindCalendarElement<T>(string name) where T : class
        {
            return FindName(name) as T;
        }

        private void CalendarHamburgerButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SetCalendarSidebarVisible(!_isCalendarSidebarVisible);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CalendarHamburgerButton_Click: {ex.Message}");
            }
        }

        private void SetCalendarSidebarVisible(bool isVisible)
        {
            _isCalendarSidebarVisible = isVisible;

            var sidebarPanel = FindCalendarElement<Border>("CalendarSidebarPanel");
            if (sidebarPanel != null)
                sidebarPanel.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;

            var sidebarColumn = FindCalendarElement<ColumnDefinition>("CalendarSidebarColumn");
            if (sidebarColumn != null)
                sidebarColumn.Width = isVisible ? new GridLength(320) : new GridLength(0);
        }

        private void CalendarCreateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MessageBox.Show("Kalender erstellen folgt im nächsten Schritt.", "Kalender", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CalendarCreateButton_Click: {ex.Message}");
            }
        }

        private void CalendarEditButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MessageBox.Show("Kalender bearbeiten folgt im nächsten Schritt.", "Kalender", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CalendarEditButton_Click: {ex.Message}");
            }
        }

        private void CalendarDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MessageBox.Show("Kalender löschen folgt im nächsten Schritt.", "Kalender", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CalendarDeleteButton_Click: {ex.Message}");
            }
        }

        private void CalendarCategoryCreateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MessageBox.Show("Kategorie erstellen folgt im nächsten Schritt.", "Kategorien", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CalendarCategoryCreateButton_Click: {ex.Message}");
            }
        }

        private void CalendarCategoryEditButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MessageBox.Show("Kategorie bearbeiten folgt im nächsten Schritt.", "Kategorien", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CalendarCategoryEditButton_Click: {ex.Message}");
            }
        }

        private void CalendarCategoryDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MessageBox.Show("Kategorie löschen folgt im nächsten Schritt.", "Kategorien", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CalendarCategoryDeleteButton_Click: {ex.Message}");
            }
        }

        private void CalendarYearBackButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _calendarFocusDate = _calendarFocusDate.AddYears(-1);
                PopulateCalendarYearList();
                UpdateCalendarUiState();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CalendarYearBackButton_Click: {ex.Message}");
            }
        }

        private void CalendarYearForwardButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _calendarFocusDate = _calendarFocusDate.AddYears(1);
                PopulateCalendarYearList();
                UpdateCalendarUiState();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CalendarYearForwardButton_Click: {ex.Message}");
            }
        }

        private void CalendarYearDropdownButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PopulateCalendarYearList();
                ToggleCalendarYearDropdown();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CalendarYearDropdownButton_Click: {ex.Message}");
            }
        }

        private void CalendarYearListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (_isUpdatingCalendarYearList)
                    return;

                var yearListBox = sender as ListBox;
                if (!(yearListBox?.SelectedItem is int selectedYear))
                    return;

                _calendarFocusDate = new DateTime(selectedYear, _calendarFocusDate.Month, Math.Min(_calendarFocusDate.Day, DateTime.DaysInMonth(selectedYear, _calendarFocusDate.Month)));
                UpdateCalendarUiState();
                var yearDropdownPanel = FindCalendarElement<Popup>("CalendarYearDropdownPanel");
                if (yearDropdownPanel != null)
                    yearDropdownPanel.IsOpen = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CalendarYearListBox_OnSelectionChanged: {ex.Message}");
            }
        }

        private void CalendarYearListBox_OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            try
            {
                if (_isUpdatingCalendarYearList)
                    return;

                if (!(sender is ListBox yearListBox))
                    return;

                if (e.VerticalOffset <= CalendarYearEdgeThreshold)
                {
                    _isUpdatingCalendarYearList = true;
                    try
                    {
                        int addedCount = AppendCalendarYearsTop(yearListBox, CalendarYearChunkSize);
                        if (addedCount > 0)
                        {
                            ScrollViewer scrollViewer = FindVisualChild<ScrollViewer>(yearListBox);
                            scrollViewer?.ScrollToVerticalOffset(e.VerticalOffset + addedCount);
                        }
                    }
                    finally
                    {
                        _isUpdatingCalendarYearList = false;
                    }
                }
                else if ((e.ExtentHeight - (e.VerticalOffset + e.ViewportHeight)) <= CalendarYearEdgeThreshold)
                {
                    _isUpdatingCalendarYearList = true;
                    try
                    {
                        AppendCalendarYearsBottom(yearListBox, CalendarYearChunkSize);
                    }
                    finally
                    {
                        _isUpdatingCalendarYearList = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CalendarYearListBox_OnScrollChanged: {ex.Message}");
            }
        }

        private static TChild FindVisualChild<TChild>(DependencyObject parent) where TChild : DependencyObject
        {
            if (parent == null)
                return null;

            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is TChild typedChild)
                    return typedChild;

                TChild descendant = FindVisualChild<TChild>(child);
                if (descendant != null)
                    return descendant;
            }

            return null;
        }

        private void CalendarDayModeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _activeCalendarViewMode = CalendarViewMode.Day;
                UpdateCalendarUiState();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CalendarDayModeButton_Click: {ex.Message}");
            }
        }

        private void CalendarWeekModeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _activeCalendarViewMode = CalendarViewMode.Week;
                UpdateCalendarUiState();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CalendarWeekModeButton_Click: {ex.Message}");
            }
        }

        private void CalendarMonthModeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _activeCalendarViewMode = CalendarViewMode.Month;
                UpdateCalendarUiState();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CalendarMonthModeButton_Click: {ex.Message}");
            }
        }

        private void OpenCalendarMainTab()
        {
            if (_mainViewTabs == null)
                return;

            var existing = _mainViewTabs.FirstOrDefault(t =>
                string.Equals(t.Key, "calendar", StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                SelectMainTab(existing);
                return;
            }

            var calendarTab = new MainViewTabState("calendar", "Kalender", canClose: true);
            _mainViewTabs.Add(calendarTab);
            SelectMainTab(calendarTab);
        }

        private void SelectMainTab(MainViewTabState tab)
        {
            if (tab == null)
                return;

            _suspendMainViewSelectionEvents = true;
            if (MainViewTabListBox != null)
                MainViewTabListBox.SelectedItem = tab;
            _suspendMainViewSelectionEvents = false;
            ApplyMainViewSelection(tab);
        }

        private void CloseMainViewTabButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var tab = (sender as Button)?.Tag as MainViewTabState;
                if (tab == null || !tab.CanClose || _mainViewTabs == null)
                    return;

                bool wasActive = ReferenceEquals(tab, _activeMainViewTab);
                _mainViewTabs.Remove(tab);
                if (wasActive)
                {
                    var fallback = _mainViewTabs.FirstOrDefault(t => string.Equals(t.Key, "basis", StringComparison.OrdinalIgnoreCase))
                                   ?? _mainViewTabs.FirstOrDefault();
                    SelectMainTab(fallback);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CloseMainViewTabButton_Click: {ex.Message}");
            }
        }

        private void MainViewTabListBox_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                TryCaptureDragStart(
                    MainViewTabListBox,
                    e,
                    out _mainTabDragStartPoint,
                    out _mainTabDragStartPointSet,
                    out _mainTabDragCandidate);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MainViewTabListBox_OnPreviewMouseLeftButtonDown: {ex.Message}");
            }
        }

        private void MainViewTabListBox_OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                if (!TryStartDrag(MainViewTabListBox, e, _mainTabDragStartPointSet, _mainTabDragStartPoint, _mainTabDragCandidate))
                    return;
                ResetMainTabDragState();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MainViewTabListBox_OnPreviewMouseMove: {ex.Message}");
                ResetMainTabDragState();
            }
        }

        private void MainViewTabListBox_OnDragOver(object sender, DragEventArgs e)
        {
            if (_mainViewTabs == null)
                return;
            e.Effects = e.Data.GetDataPresent(typeof(MainViewTabState))
                ? DragDropEffects.Move
                : DragDropEffects.None;
            e.Handled = true;
        }

        private void MainViewTabListBox_OnDrop(object sender, DragEventArgs e)
        {
            try
            {
                if (_mainViewTabs == null)
                    return;
                ReorderFromDrop<MainViewTabState>(_mainViewTabs, e);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MainViewTabListBox_OnDrop: {ex.Message}");
            }
            finally
            {
                ResetMainTabDragState();
            }
        }

        private void CreateFolderCell_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            CreateFolderButton_Click(sender, new RoutedEventArgs());
            e.Handled = true;
        }

        private static string BuildNextNewFolderName(string parentDirectory)
        {
            const string baseName = "Neuer Ordner";
            if (string.IsNullOrEmpty(parentDirectory) || !Directory.Exists(parentDirectory))
                return baseName;

            string candidate = baseName;
            int suffix = 2;
            while (Directory.Exists(Path.Combine(parentDirectory, candidate)))
            {
                candidate = $"{baseName} ({suffix})";
                suffix++;
            }

            return candidate;
        }


        private void NavigateIntoFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
                return;
            string full = Path.GetFullPath(folderPath);
            if (!full.StartsWith(Path.GetFullPath(_storage.WorkspaceRootPath), StringComparison.OrdinalIgnoreCase))
                return;
            _currentDesktopDirectory = full;
            ClearDesktopSelection();
            RefreshDesktopSurface();
        }

        /// <summary>Baut die Desktop-Icons für <see cref="_currentDesktopDirectory"/> neu auf.</summary>
        private void RefreshDesktopSurface()
        {
            try
            {
                if (DesktopCanvas == null || _layoutService == null)
                    return;
                if (string.IsNullOrEmpty(_currentDesktopDirectory) || !Directory.Exists(_currentDesktopDirectory))
                    _currentDesktopDirectory = Path.GetFullPath(_storage.WorkspaceRootPath);
                DesktopCanvas.Children.Clear();
                ClearDesktopSelection();
                var items = _storage.ListDesktopItemsInDirectory(_currentDesktopDirectory);
                int slot = 0;
                foreach (var item in items)
                {
                    string key = _layoutService.ToLayoutKey(item.FullPath);
                    var pos = GetOrCreateLayoutPoint(key, slot);
                    Border icon = CreateDesktopIconBorder(item, pos.X, pos.Y);
                    DesktopCanvas.Children.Add(icon);
                    slot++;
                }
                PersistDesktopLayout();
                Dispatcher.BeginInvoke(new Action(ResizeDesktopCanvasExtent), DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RefreshDesktopSurface: {ex.Message}");
            }
        }

        private WorkspaceDesktopPointDto GetOrCreateLayoutPoint(string layoutKey, int slotIndex)
        {
            if (_desktopLayout.Positions != null &&
                _desktopLayout.Positions.TryGetValue(layoutKey, out var existing) &&
                existing != null)
                return existing;
            int col = slotIndex % 6;
            int row = slotIndex / 6;
            var dto = new WorkspaceDesktopPointDto
            {
                X = 12 + col * DesktopIconSlotW,
                Y = 12 + row * DesktopIconSlotH
            };
            if (_desktopLayout.Positions == null)
                _desktopLayout.Positions = new Dictionary<string, WorkspaceDesktopPointDto>(StringComparer.OrdinalIgnoreCase);
            _desktopLayout.Positions[layoutKey] = dto;
            return dto;
        }

        /// <summary>Erzeugt ein verschiebbares Ordner- bzw. Datei-Symbol.</summary>
        private Border CreateDesktopIconBorder(WorkspaceDesktopItemInfo item, double x, double y)
        {
            var border = new Border
            {
                Width = 96,
                Padding = new Thickness(4),
                Background = Brushes.White,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)),
                CornerRadius = new CornerRadius(6),
                Cursor = Cursors.Hand,
                Tag = new DesktopIconContext(item)
            };
            Canvas.SetLeft(border, x);
            Canvas.SetTop(border, y);

            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };
            var glyph = new TextBlock
            {
                Text = item.IsFolder ? "\uD83D\uDCC1" : "\uD83D\uDCC4",
                FontSize = 40,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            var label = new TextBlock
            {
                Text = item.DisplayName,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                MaxWidth = 88,
                FontSize = 11
            };
            var renameTextBox = new TextBox
            {
                Text = item.DisplayName,
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 2, 0, 0),
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            renameTextBox.TextChanged += RenameTextBox_TextChanged;
            renameTextBox.PreviewKeyDown += RenameTextBox_PreviewKeyDown;
            stack.Children.Add(glyph);
            stack.Children.Add(label);
            stack.Children.Add(renameTextBox);

            var renameTriggerButton = CreateIconActionButton("\u270E", Brushes.Transparent, new SolidColorBrush(Color.FromRgb(96, 96, 96)));
            renameTriggerButton.Width = 24;
            renameTriggerButton.Height = 24;
            renameTriggerButton.Margin = new Thickness(0, 6, 0, 0);
            renameTriggerButton.HorizontalAlignment = HorizontalAlignment.Center;
            renameTriggerButton.Visibility = Visibility.Collapsed;
            renameTriggerButton.Click += RenameTriggerButton_Click;
            stack.Children.Add(renameTriggerButton);

            var renameConfirmGrid = new Grid
            {
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 6, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            renameConfirmGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            renameConfirmGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var renameConfirmButton = CreateIconActionButton("\u2713", Brushes.Transparent, new SolidColorBrush(Color.FromRgb(52, 153, 76)));
            renameConfirmButton.ToolTip = "Umbenennen bestätigen";
            renameConfirmButton.Click += RenameConfirmButton_Click;
            Grid.SetColumn(renameConfirmButton, 0);
            renameConfirmGrid.Children.Add(renameConfirmButton);

            var renameCancelButton = CreateIconActionButton("\u00d7", Brushes.Transparent, new SolidColorBrush(Color.FromRgb(208, 52, 52)));
            renameCancelButton.ToolTip = "Umbenennen abbrechen";
            renameCancelButton.Click += RenameCancelButton_Click;
            Grid.SetColumn(renameCancelButton, 1);
            renameConfirmGrid.Children.Add(renameCancelButton);
            stack.Children.Add(renameConfirmGrid);

            var deleteTriggerButton = CreateIconActionButton("\u00d7", Brushes.Transparent, new SolidColorBrush(Color.FromRgb(208, 52, 52)));
            ConfigureIconTriggerButton(deleteTriggerButton);
            deleteTriggerButton.Visibility = Visibility.Collapsed;
            deleteTriggerButton.ToolTip = "Löschen";
            deleteTriggerButton.Click += DeleteTriggerButton_Click;
            stack.Children.Add(deleteTriggerButton);

            var deleteConfirmGrid = new Grid
            {
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 6, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            deleteConfirmGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            deleteConfirmGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var confirmButton = CreateIconActionButton("\u2713", Brushes.Transparent, new SolidColorBrush(Color.FromRgb(52, 153, 76)));
            confirmButton.ToolTip = "Löschen bestätigen";
            confirmButton.Click += DeleteConfirmButton_Click;
            Grid.SetColumn(confirmButton, 0);
            deleteConfirmGrid.Children.Add(confirmButton);

            var cancelButton = CreateIconActionButton("\u00d7", Brushes.Transparent, new SolidColorBrush(Color.FromRgb(208, 52, 52)));
            cancelButton.ToolTip = "Abbrechen";
            cancelButton.Click += DeleteCancelButton_Click;
            Grid.SetColumn(cancelButton, 1);
            deleteConfirmGrid.Children.Add(cancelButton);
            stack.Children.Add(deleteConfirmGrid);

            if (border.Tag is DesktopIconContext context)
            {
                context.Label = label;
                context.RenameTextBox = renameTextBox;
                context.RenameTriggerButton = renameTriggerButton;
                context.RenameConfirmGrid = renameConfirmGrid;
                context.DeleteTriggerButton = deleteTriggerButton;
                context.DeleteConfirmGrid = deleteConfirmGrid;
            }

            border.Child = stack;

            border.MouseLeftButtonDown += DesktopIcon_MouseLeftButtonDown;
            border.MouseRightButtonDown += DesktopIcon_MouseRightButtonDown;
            border.MouseEnter += (_, __) => border.Background = new SolidColorBrush(Color.FromRgb(255, 246, 204));
            border.MouseLeave += (_, __) => border.Background = Brushes.White;
            return border;
        }

        private static Button CreateIconActionButton(string content, Brush background, Brush hoverBrush)
        {
            var button = new Button
            {
                Content = content,
                Padding = new Thickness(0),
                Height = 24,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                Background = background,
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 128, 128, 128)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            button.MouseEnter += (_, __) => button.Background = hoverBrush;
            button.MouseLeave += (_, __) => button.Background = background;
            return button;
        }

        private static void ConfigureIconTriggerButton(Button button)
        {
            if (button == null)
                return;

            button.Width = 24;
            button.Height = 24;
            button.Margin = new Thickness(0, 6, 0, 0);
            button.HorizontalAlignment = HorizontalAlignment.Center;
        }

        private void DesktopIcon_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.OriginalSource is Button)
                {
                    e.Handled = true;
                    return;
                }
                if (!(sender is Border border))
                    return;
                WorkspaceDesktopItemInfo item = GetDesktopItem(border);
                if (item == null)
                    return;
                if (e.ClickCount == 2)
                {
                    e.Handled = true;
                    _ = HandleDesktopIconActivateAsync(item);
                    return;
                }
                SelectDesktopIcon(border);
                CloseDeleteMenu();
                ShowRenameTriggerFor(border);
                _pendingDragIcon = border;
                _isDesktopDragActive = false;
                _pendingDragPointerStart = e.GetPosition(DesktopCanvas);
                _pendingDragIconOrigin = new Point(Canvas.GetLeft(border), Canvas.GetTop(border));
                e.Handled = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DesktopIcon_MouseLeftButtonDown: {ex.Message}");
            }
        }

        private void DesktopIcon_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (!(sender is Border border))
                    return;
                WorkspaceDesktopItemInfo item = GetDesktopItem(border);
                if (item == null)
                    return;
                SelectDesktopIcon(border);
                CloseRenameMenu();
                ShowDeleteTriggerFor(border);
                _pendingDragIcon = null;
                _isDesktopDragActive = false;
                e.Handled = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DesktopIcon_MouseRightButtonDown: {ex.Message}");
            }
        }

        /// <summary>Doppelklick: Ordner öffnen bzw. Notiz in einem Tab laden.</summary>
        private async Task HandleDesktopIconActivateAsync(WorkspaceDesktopItemInfo item)
        {
            try
            {
                if (item == null)
                    return;
                if (item.IsFolder)
                    NavigateIntoFolder(item.FullPath);
                else
                    await OpenOrActivateTabAsync(item.FullPath).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"HandleDesktopIconActivateAsync: {ex.Message}");
            }
        }

        private void DesktopCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                if (_pendingDragIcon == null || e.LeftButton != MouseButtonState.Pressed)
                    return;
                Point now = e.GetPosition(DesktopCanvas);
                double dx = now.X - _pendingDragPointerStart.X;
                double dy = now.Y - _pendingDragPointerStart.Y;
                if (!_isDesktopDragActive)
                {
                    if (dx * dx + dy * dy < DesktopDragThresholdSq)
                        return;
                    _isDesktopDragActive = true;
                    DesktopCanvas.CaptureMouse();
                }
                Canvas.SetLeft(_pendingDragIcon, _pendingDragIconOrigin.X + dx);
                Canvas.SetTop(_pendingDragIcon, _pendingDragIconOrigin.Y + dy);
                ResizeDesktopCanvasExtent();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DesktopCanvas_MouseMove: {ex.Message}");
            }
        }

        private void DesktopCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                FinishDesktopDragIfAny();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DesktopCanvas_MouseLeftButtonUp: {ex.Message}");
            }
        }

        /// <summary>Beendet Drag: optional Notiz per Drop auf Ordner-Symbol verschieben, sonst nur Layout speichern.</summary>
        private void FinishDesktopDragIfAny()
        {
            if (_pendingDragIcon == null)
                return;
            if (_isDesktopDragActive)
            {
                try
                {
                    WorkspaceDesktopItemInfo dragged = GetDesktopItem(_pendingDragIcon);
                    if (dragged != null && !dragged.IsFolder)
                    {
                        WorkspaceDesktopItemInfo targetFolder = FindFolderIconAtDropCenter(_pendingDragIcon);
                        if (targetFolder != null &&
                            TryMoveNoteFileIntoFolder(dragged.FullPath, targetFolder.FullPath))
                        {
                            DesktopCanvas.ReleaseMouseCapture();
                            _pendingDragIcon = null;
                            _isDesktopDragActive = false;
                            RefreshDesktopSurface();
                            RefreshTabListDisplay();
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"FinishDesktopDragIfAny move: {ex.Message}");
                }
                SaveLayoutForIconBorder(_pendingDragIcon);
                PersistDesktopLayout();
                DesktopCanvas.ReleaseMouseCapture();
            }
            _pendingDragIcon = null;
            _isDesktopDragActive = false;
        }

        /// <summary>
        /// Ordner-Symbol unter dem Mittelpunkt des gezogenen Symbols (nicht die Mausposition — vermeidet Treffer auf das Icon selbst).
        /// </summary>
        private WorkspaceDesktopItemInfo FindFolderIconAtDropCenter(Border draggedBorder)
        {
            if (draggedBorder == null || DesktopCanvas == null)
                return null;
            double w = draggedBorder.ActualWidth > 0 ? draggedBorder.ActualWidth : draggedBorder.Width;
            double h = draggedBorder.ActualHeight > 0 ? draggedBorder.ActualHeight : draggedBorder.Height;
            if (w <= 0) w = 96;
            if (h <= 0) h = 96;
            double cx = Canvas.GetLeft(draggedBorder) + w * 0.5;
            double cy = Canvas.GetTop(draggedBorder) + h * 0.5;
            return FindFolderIconAtPoint(new Point(cx, cy), draggedBorder);
        }

        /// <summary>Liefert das oberste Ordner-Symbol unter <paramref name="canvasPoint"/> (Z-Order: zuletzt gezeichnet).</summary>
        private WorkspaceDesktopItemInfo FindFolderIconAtPoint(Point canvasPoint, Border excludeBorder)
        {
            for (int i = DesktopCanvas.Children.Count - 1; i >= 0; i--)
            {
                if (!(DesktopCanvas.Children[i] is Border b) || ReferenceEquals(b, excludeBorder))
                    continue;
                WorkspaceDesktopItemInfo info = GetDesktopItem(b);
                if (info == null || !info.IsFolder)
                    continue;
                double left = Canvas.GetLeft(b);
                double top = Canvas.GetTop(b);
                double bw = b.ActualWidth > 0 ? b.ActualWidth : b.Width;
                double bh = b.ActualHeight > 0 ? b.ActualHeight : b.Height;
                if (bw <= 0) bw = 96;
                if (bh <= 0) bh = 96;
                if (canvasPoint.X >= left && canvasPoint.X <= left + bw &&
                    canvasPoint.Y >= top && canvasPoint.Y <= top + bh)
                    return info;
            }
            return null;
        }

        /// <summary>Verschiebt eine Notizdatei physisch in den Zielordner und passt Layout sowie offene Tabs an.</summary>
        private bool TryMoveNoteFileIntoFolder(string sourceFilePath, string targetFolderPath)
        {
            try
            {
                if (string.IsNullOrEmpty(sourceFilePath) || string.IsNullOrEmpty(targetFolderPath))
                    return false;
                if (!_storage.IsNoteFileInWorkspace(sourceFilePath))
                    return false;
                string fullFolder = Path.GetFullPath(targetFolderPath);
                if (!fullFolder.StartsWith(Path.GetFullPath(_storage.WorkspaceRootPath), StringComparison.OrdinalIgnoreCase) ||
                    !Directory.Exists(fullFolder))
                    return false;
                string sourceDir = Path.GetDirectoryName(sourceFilePath);
                if (string.Equals(fullFolder, sourceDir, StringComparison.OrdinalIgnoreCase))
                    return false;
                string fileName = Path.GetFileName(sourceFilePath);
                string destPath = Path.Combine(fullFolder, fileName);
                if (File.Exists(destPath))
                {
                    MessageBox.Show(
                        "Im Zielordner existiert bereits eine Datei mit diesem Namen.",
                        "Workspace",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return false;
                }
                File.Move(sourceFilePath, destPath);
                RemoveLayoutEntryForPath(sourceFilePath);
                UpdateOpenTabsPathAfterFileMove(sourceFilePath, destPath);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TryMoveNoteFileIntoFolder: {ex.Message}");
                MessageBox.Show($"Datei konnte nicht verschoben werden: {ex.Message}", "Workspace", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }

        private void RemoveLayoutEntryForPath(string absolutePath)
        {
            if (_layoutService == null || _desktopLayout?.Positions == null || string.IsNullOrEmpty(absolutePath))
                return;
            string key = _layoutService.ToLayoutKey(absolutePath);
            if (_desktopLayout.Positions.ContainsKey(key))
                _desktopLayout.Positions.Remove(key);
        }

        private void UpdateOpenTabsPathAfterFileMove(string oldPath, string newPath)
        {
            if (_openTabs == null)
                return;
            foreach (var t in _openTabs)
            {
                if (string.Equals(t.FilePath, oldPath, StringComparison.OrdinalIgnoreCase))
                {
                    t.FilePath = newPath;
                    t.Title = Path.GetFileName(newPath);
                }
            }
        }

        /// <summary>Aktualisiert Tab-Beschriftungen nach Pfadänderung (POCO-Items ohne INotifyPropertyChanged).</summary>
        private void RefreshTabListDisplay()
        {
            try
            {
                TabListBox?.Items.Refresh();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RefreshTabListDisplay: {ex.Message}");
            }
        }

        private void DesktopCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.OriginalSource == DesktopCanvas)
                    ClearDesktopSelection();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DesktopCanvas_MouseLeftButtonDown: {ex.Message}");
            }
        }

        private void SaveLayoutForIconBorder(Border border)
        {
            WorkspaceDesktopItemInfo item = GetDesktopItem(border);
            if (item != null && _layoutService != null)
            {
                string key = _layoutService.ToLayoutKey(item.FullPath);
                if (_desktopLayout.Positions == null)
                    _desktopLayout.Positions = new Dictionary<string, WorkspaceDesktopPointDto>(StringComparer.OrdinalIgnoreCase);
                _desktopLayout.Positions[key] = new WorkspaceDesktopPointDto
                {
                    X = Canvas.GetLeft(border),
                    Y = Canvas.GetTop(border)
                };
            }
        }

        private void SelectDesktopIcon(Border border)
        {
            if (_selectedDesktopIcon != null)
            {
                _selectedDesktopIcon.BorderBrush = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128));
                _selectedDesktopIcon.BorderThickness = new Thickness(1);
            }
            _selectedDesktopIcon = border;
            if (_selectedDesktopIcon != null)
            {
                _selectedDesktopIcon.BorderBrush = SystemColors.HighlightBrush;
                _selectedDesktopIcon.BorderThickness = new Thickness(2);
            }
        }

        private void ClearDesktopSelection()
        {
            CloseDeleteMenu();
            CloseRenameMenu();
            if (_selectedDesktopIcon != null)
            {
                _selectedDesktopIcon.BorderBrush = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128));
                _selectedDesktopIcon.BorderThickness = new Thickness(1);
            }
            _selectedDesktopIcon = null;
        }

        private WorkspaceDesktopItemInfo GetDesktopItem(Border border)
        {
            return (border?.Tag as DesktopIconContext)?.Item;
        }

        private void ShowDeleteTriggerFor(Border border)
        {
            foreach (UIElement child in DesktopCanvas.Children)
            {
                if (child is Border other && other.Tag is DesktopIconContext ctx)
                {
                    ctx.DeleteTriggerButton.Visibility = ReferenceEquals(other, border)
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                    ctx.DeleteConfirmGrid.Visibility = Visibility.Collapsed;
                }
            }
            _deleteMenuOwnerIcon = null;
        }

        private void ShowRenameTriggerFor(Border border)
        {
            foreach (UIElement child in DesktopCanvas.Children)
            {
                if (child is Border other && other.Tag is DesktopIconContext ctx)
                {
                    bool isCurrent = ReferenceEquals(other, border);
                    ctx.IsRenameEditing = false;
                    ctx.RenameTextBox.Visibility = Visibility.Collapsed;
                    ctx.RenameConfirmGrid.Visibility = Visibility.Collapsed;
                    ctx.Label.Visibility = Visibility.Visible;
                    ctx.RenameTriggerButton.Visibility = isCurrent ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            _renameMenuOwnerIcon = null;
        }

        private void CloseRenameMenu()
        {
            foreach (UIElement child in DesktopCanvas.Children)
            {
                if (child is Border b && b.Tag is DesktopIconContext ctx)
                {
                    ctx.IsRenameEditing = false;
                    if (ctx.RenameTextBox != null)
                        ctx.RenameTextBox.Text = ctx.Item?.DisplayName ?? string.Empty;
                    ctx.RenameTextBox.Visibility = Visibility.Collapsed;
                    ctx.RenameConfirmGrid.Visibility = Visibility.Collapsed;
                    ctx.RenameTriggerButton.Visibility = Visibility.Collapsed;
                    ctx.Label.Visibility = Visibility.Visible;
                }
            }
            _renameMenuOwnerIcon = null;
        }

        private void EnterRenameMode(Border border)
        {
            if (!(border?.Tag is DesktopIconContext ctx) || ctx.Item == null)
                return;

            CloseDeleteMenu();
            ShowRenameTriggerFor(border);
            ctx.IsRenameEditing = true;
            ctx.RenameTriggerButton.Visibility = Visibility.Collapsed;
            ctx.Label.Visibility = Visibility.Collapsed;
            ctx.RenameTextBox.Visibility = Visibility.Visible;
            ctx.RenameTextBox.Text = ctx.Item.DisplayName ?? string.Empty;
            ctx.RenameTextBox.Focus();
            ctx.RenameTextBox.SelectAll();
            ctx.RenameConfirmGrid.Visibility = Visibility.Collapsed;
            _renameMenuOwnerIcon = border;
        }

        private void UpdateRenameConfirmVisibility(DesktopIconContext ctx)
        {
            if (ctx == null || !ctx.IsRenameEditing)
                return;
            string currentName = ctx.Item?.DisplayName ?? string.Empty;
            string proposed = ctx.RenameTextBox?.Text ?? string.Empty;
            bool changed = !string.Equals(currentName, proposed, StringComparison.Ordinal);
            ctx.RenameConfirmGrid.Visibility = changed ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ShowDeleteConfirmFor(Border border)
        {
            if (!(border?.Tag is DesktopIconContext ctx))
                return;
            ShowDeleteTriggerFor(border);
            ctx.DeleteTriggerButton.Visibility = Visibility.Collapsed;
            ctx.DeleteConfirmGrid.Visibility = Visibility.Visible;
            _deleteMenuOwnerIcon = border;
        }

        private void CloseDeleteMenu()
        {
            foreach (UIElement child in DesktopCanvas.Children)
            {
                if (child is Border b && b.Tag is DesktopIconContext ctx)
                {
                    ctx.DeleteTriggerButton.Visibility = Visibility.Collapsed;
                    ctx.DeleteConfirmGrid.Visibility = Visibility.Collapsed;
                }
            }
            _deleteMenuOwnerIcon = null;
        }

        private void DeleteTriggerButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Border owner = FindOwningDesktopBorder(sender as DependencyObject);
                if (owner == null)
                    return;
                SelectDesktopIcon(owner);
                ShowDeleteConfirmFor(owner);
                e.Handled = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DeleteTriggerButton_Click: {ex.Message}");
            }
        }

        private void RenameTriggerButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Border owner = FindOwningDesktopBorder(sender as DependencyObject);
                if (owner == null)
                    return;
                SelectDesktopIcon(owner);
                EnterRenameMode(owner);
                e.Handled = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RenameTriggerButton_Click: {ex.Message}");
            }
        }

        private void RenameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                Border owner = FindOwningDesktopBorder(sender as DependencyObject);
                if (!(owner?.Tag is DesktopIconContext ctx))
                    return;
                UpdateRenameConfirmVisibility(ctx);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RenameTextBox_TextChanged: {ex.Message}");
            }
        }

        private async void RenameConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Border owner = FindOwningDesktopBorder(sender as DependencyObject);
                if (!(owner?.Tag is DesktopIconContext ctx) || ctx.Item == null)
                    return;
                string proposedName = (ctx.RenameTextBox?.Text ?? string.Empty).Trim();
                await RenameDesktopItemAsync(ctx.Item, proposedName).ConfigureAwait(true);
                e.Handled = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RenameConfirmButton_Click: {ex.Message}");
                MessageBox.Show(ex.Message, "Workspace", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void RenameCancelButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_renameMenuOwnerIcon != null)
                    ShowRenameTriggerFor(_renameMenuOwnerIcon);
                e.Handled = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RenameCancelButton_Click: {ex.Message}");
            }
        }

        private void RenameTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.Key == Key.Escape)
                {
                    if (_renameMenuOwnerIcon != null)
                        ShowRenameTriggerFor(_renameMenuOwnerIcon);
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.Enter)
                {
                    Border owner = FindOwningDesktopBorder(sender as DependencyObject);
                    if (!(owner?.Tag is DesktopIconContext ctx) || ctx.Item == null)
                        return;
                    _ = RenameDesktopItemAsync(ctx.Item, (ctx.RenameTextBox?.Text ?? string.Empty).Trim());
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RenameTextBox_PreviewKeyDown: {ex.Message}");
            }
        }

        private async void DeleteConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Border owner = FindOwningDesktopBorder(sender as DependencyObject);
                WorkspaceDesktopItemInfo selected = GetDesktopItem(owner);
                if (selected == null)
                    return;

                if (selected.IsFolder)
                {
                    await DeleteFolderAsync(selected.FullPath).ConfigureAwait(true);
                }
                else
                {
                    await DeleteFileAsync(selected.FullPath).ConfigureAwait(true);
                }
                e.Handled = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DeleteConfirmButton_Click: {ex.Message}");
                MessageBox.Show(ex.Message, "Workspace", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void DeleteCancelButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_deleteMenuOwnerIcon != null)
                    ShowDeleteTriggerFor(_deleteMenuOwnerIcon);
                e.Handled = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DeleteCancelButton_Click: {ex.Message}");
            }
        }

        private async Task DeleteFileAsync(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                    return;
                var open = _openTabs?.FirstOrDefault(t => string.Equals(t.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
                if (open != null)
                    await RemoveTabInternal(open, saveFirst: false).ConfigureAwait(true);
                _storage.DeleteNoteFile(filePath);
                RemoveLayoutEntryForPath(filePath);
                CloseDeleteMenu();
                RefreshDesktopSurface();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DeleteFileAsync: {ex.Message}");
                throw;
            }
        }

        private async Task RenameDesktopItemAsync(WorkspaceDesktopItemInfo item, string newName)
        {
            try
            {
                if (item == null)
                    return;
                string oldPath = item.FullPath;
                if (string.IsNullOrEmpty(oldPath))
                    return;
                string originalName = item.DisplayName ?? string.Empty;
                if (string.IsNullOrWhiteSpace(newName))
                    throw new InvalidOperationException("Der Name darf nicht leer sein.");
                if (string.Equals(originalName, newName, StringComparison.Ordinal))
                {
                    if (_renameMenuOwnerIcon != null)
                        ShowRenameTriggerFor(_renameMenuOwnerIcon);
                    return;
                }
                if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    throw new InvalidOperationException("Der Name enthält ungültige Zeichen.");

                string parent = Path.GetDirectoryName(oldPath);
                if (string.IsNullOrEmpty(parent))
                    return;
                string newPath = Path.Combine(parent, newName);
                if (File.Exists(newPath) || Directory.Exists(newPath))
                    throw new InvalidOperationException("In diesem Ordner existiert bereits ein Element mit diesem Namen.");

                if (item.IsFolder)
                {
                    Directory.Move(oldPath, newPath);
                    await UpdateOpenTabsPathAfterFolderMoveAsync(oldPath, newPath).ConfigureAwait(true);
                }
                else
                {
                    File.Move(oldPath, newPath);
                    UpdateOpenTabsPathAfterFileMove(oldPath, newPath);
                }

                MoveLayoutEntry(oldPath, newPath);
                item.FullPath = newPath;
                item.DisplayName = newName;
                CloseRenameMenu();
                RefreshDesktopSurface();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RenameDesktopItemAsync: {ex.Message}");
                throw;
            }
        }

        private async Task UpdateOpenTabsPathAfterFolderMoveAsync(string oldFolderPath, string newFolderPath)
        {
            if (_openTabs == null)
                return;
            string oldPrefix = oldFolderPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string newPrefix = newFolderPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            foreach (var tab in _openTabs.ToList())
            {
                if (!tab.FilePath.StartsWith(oldPrefix, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (tab == _activeTab)
                    CaptureCanvasToTab(tab);
                await SaveTabAsync(tab).ConfigureAwait(true);
                string suffix = tab.FilePath.Substring(oldPrefix.Length);
                string remapped = Path.Combine(newPrefix, suffix);
                tab.FilePath = remapped;
                tab.Title = Path.GetFileName(remapped);
            }
            RefreshTabListDisplay();
        }

        private void MoveLayoutEntry(string oldPath, string newPath)
        {
            if (_desktopLayout?.Positions == null || _layoutService == null)
                return;

            var current = _desktopLayout.Positions.ToList();
            foreach (var kvp in current)
            {
                string oldKey = _layoutService.ToLayoutKey(oldPath);
                if (string.Equals(kvp.Key, oldKey, StringComparison.OrdinalIgnoreCase))
                {
                    _desktopLayout.Positions.Remove(kvp.Key);
                    _desktopLayout.Positions[_layoutService.ToLayoutKey(newPath)] = kvp.Value;
                    return;
                }
            }
        }

        private async Task DeleteFolderAsync(string folderPath)
        {
            try
            {
                if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
                    return;
                await CloseTabsUnderPathAsync(folderPath).ConfigureAwait(true);
                _storage.DeleteFolderRecursive(folderPath);
                CloseDeleteMenu();
                RefreshDesktopSurface();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DeleteFolderAsync: {ex.Message}");
                throw;
            }
        }

        private async Task CloseTabsUnderPathAsync(string directoryPrefix)
        {
            if (_openTabs == null)
                return;
            string prefix = directoryPrefix.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var toClose = _openTabs
                .Where(t => t.FilePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToList();
            foreach (var tab in toClose)
                await RemoveTabInternal(tab, saveFirst: true).ConfigureAwait(true);
        }

        private Border FindOwningDesktopBorder(DependencyObject origin)
        {
            DependencyObject current = origin;
            while (current != null)
            {
                if (current is Border border && border.Tag is DesktopIconContext)
                    return border;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private void ResizeDesktopCanvasExtent()
        {
            try
            {
                double vw = DesktopCanvas?.ActualWidth > 0
                    ? DesktopCanvas.ActualWidth
                    : Math.Max(ActualWidth, 400);
                double vh = DesktopCanvas?.ActualHeight > 0
                    ? DesktopCanvas.ActualHeight
                    : Math.Max(ActualHeight, 300);
                double maxR = vw;
                double maxB = vh;
                foreach (UIElement child in DesktopCanvas.Children)
                {
                    if (child is FrameworkElement fe)
                    {
                        double l = Canvas.GetLeft(fe);
                        double t = Canvas.GetTop(fe);
                        double w = fe.ActualWidth > 0 ? fe.ActualWidth : fe.Width;
                        double h = fe.ActualHeight > 0 ? fe.ActualHeight : fe.Height;
                        maxR = Math.Max(maxR, l + w + 48);
                        maxB = Math.Max(maxB, t + h + 48);
                    }
                }
                DesktopCanvas.Width = Math.Max(maxR, vw + 1);
                DesktopCanvas.Height = Math.Max(maxB, vh + 1);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ResizeDesktopCanvasExtent: {ex.Message}");
            }
        }

        private void ShowEditorChrome()
        {
            EditorChrome.Visibility = Visibility.Visible;
        }

        private void HideEditorChromeIfNoTabs()
        {
            if (_openTabs == null || _openTabs.Count == 0)
            {
                EditorChrome.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateSessionActiveFlag()
        {
            if (_viewModel == null)
                return;
            _viewModel.IsInkSessionActive = _activeTab != null;
        }

        private async void TabListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suspendTabSelectionEvents)
                return;
            var next = TabListBox.SelectedItem as WorkspaceDocumentTabState;
            await OnTabSelectionChangedCoreAsync(next, savePrevious: true).ConfigureAwait(true);
        }

        private void TabListBox_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                TryCaptureDragStart(
                    TabListBox,
                    e,
                    out _tabDragStartPoint,
                    out _tabDragStartPointSet,
                    out _tabDragCandidate);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TabListBox_OnPreviewMouseLeftButtonDown: {ex.Message}");
            }
        }

        private void TabListBox_OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                if (!TryStartDrag(TabListBox, e, _tabDragStartPointSet, _tabDragStartPoint, _tabDragCandidate))
                    return;
                ResetTabDragState();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TabListBox_OnPreviewMouseMove: {ex.Message}");
                ResetTabDragState();
            }
        }

        private void TabListBox_OnDragOver(object sender, DragEventArgs e)
        {
            if (_openTabs == null)
                return;
            e.Effects = e.Data.GetDataPresent(typeof(WorkspaceDocumentTabState))
                ? DragDropEffects.Move
                : DragDropEffects.None;
            e.Handled = true;
        }

        private void TabListBox_OnDrop(object sender, DragEventArgs e)
        {
            try
            {
                if (_openTabs == null)
                    return;
                ReorderFromDrop<WorkspaceDocumentTabState>(_openTabs, e);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TabListBox_OnDrop: {ex.Message}");
            }
            finally
            {
                ResetTabDragState();
            }
        }

        // Reihenfolge ist wichtig: zuerst aktuellen Tab in den Cache spiegeln, dann persistieren, erst danach UI auf den Ziel-Tab schalten.
        private void OnTabSelectionChangedCore(WorkspaceDocumentTabState next, bool savePrevious)
            => _ = ChangeTabSelectionAsync(next, savePrevious, saveSynchronously: false, hideEditorWhenNoTabs: false);

        private async Task OnTabSelectionChangedCoreAsync(WorkspaceDocumentTabState next, bool savePrevious)
            => await ChangeTabSelectionAsync(next, savePrevious, saveSynchronously: true, hideEditorWhenNoTabs: true).ConfigureAwait(true);

        private async Task ChangeTabSelectionAsync(
            WorkspaceDocumentTabState next,
            bool savePrevious,
            bool saveSynchronously,
            bool hideEditorWhenNoTabs)
        {
            try
            {
                if (next == _activeTab)
                    return;
                if (savePrevious && _activeTab != null)
                {
                    CaptureCanvasToTab(_activeTab);
                    if (saveSynchronously)
                        await SaveTabAsync(_activeTab).ConfigureAwait(true);
                    else
                        _ = SaveTabAsync(_activeTab);
                }
                ApplyTabSelectionCore(next, hideEditorWhenNoTabs);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ChangeTabSelectionAsync: {ex.Message}");
            }
        }

        private void ChangeTabSelectionSync(
            WorkspaceDocumentTabState next,
            bool savePrevious,
            bool hideEditorWhenNoTabs)
        {
            try
            {
                if (next == _activeTab)
                    return;
                if (savePrevious && _activeTab != null)
                {
                    CaptureCanvasToTab(_activeTab);
                    SaveTabToDiskSync(_activeTab);
                }
                ApplyTabSelectionCore(next, hideEditorWhenNoTabs);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ChangeTabSelectionSync: {ex.Message}");
            }
        }

        private void ApplyTabSelectionCore(WorkspaceDocumentTabState next, bool hideEditorWhenNoTabs)
        {
            _activeTab = next;
            if (_activeTab == null)
            {
                WorkspaceInkCanvas.Strokes.Clear();
                UpdateSessionActiveFlag();
                if (hideEditorWhenNoTabs)
                    HideEditorChromeIfNoTabs();
                SyncWorkspaceCanvasSize();
                return;
            }
            ApplyTabToCanvas(_activeTab);
            UpdateSessionActiveFlag();
            ShowEditorChrome();
            SyncWorkspaceCanvasSize();
        }

        /// <summary>Synchrones Öffnen/Aktivieren (z. B. vor PEN_UP einer neuen Notiz).</summary>
        private void OpenOrActivateTabSync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !_storage.IsNoteFileInWorkspace(filePath))
                return;
            try
            {
                var existing = _openTabs?.FirstOrDefault(t =>
                    string.Equals(t.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    _suspendTabSelectionEvents = true;
                    TabListBox.SelectedItem = existing;
                    _suspendTabSelectionEvents = false;
                    ChangeTabSelectionSync(existing, savePrevious: true, hideEditorWhenNoTabs: true);
                    return;
                }

                var payload = _storage.ReadDocumentSync(filePath);
                var tab = new WorkspaceDocumentTabState
                {
                    FilePath = filePath,
                    Title = Path.GetFileName(filePath),
                    CachedIsfBase64 = payload.InkIsfBase64 ?? string.Empty,
                    RecognizedTextUtf8 = payload.RecognizedTextUtf8 ?? string.Empty
                };
                _suspendTabSelectionEvents = true;
                _openTabs.Add(tab);
                TabListBox.SelectedItem = tab;
                _suspendTabSelectionEvents = false;
                ChangeTabSelectionSync(tab, savePrevious: true, hideEditorWhenNoTabs: true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OpenOrActivateTabSync: {ex.Message}");
                MessageBox.Show($"Datei konnte nicht geöffnet werden: {ex.Message}", "Workspace", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static void TryCaptureDragStart<T>(
            UIElement dragHost,
            MouseButtonEventArgs e,
            out Point startPoint,
            out bool hasStartPoint,
            out T candidate) where T : class
        {
            if (dragHost == null)
            {
                startPoint = default;
                hasStartPoint = false;
                candidate = null;
                return;
            }

            startPoint = e.GetPosition(dragHost);
            hasStartPoint = true;
            candidate = ResolveItemFromVisual<T>(e.OriginalSource as DependencyObject);
        }

        private static bool TryStartDrag<T>(
            UIElement dragHost,
            MouseEventArgs e,
            bool hasStartPoint,
            Point startPoint,
            T candidate) where T : class
        {
            if (!hasStartPoint || candidate == null || dragHost == null || e.LeftButton != MouseButtonState.Pressed)
                return false;

            Point current = e.GetPosition(dragHost);
            if (!ShouldStartDrag(startPoint, current))
                return false;

            var dragData = new DataObject(typeof(T), candidate);
            DragDrop.DoDragDrop(dragHost, dragData, DragDropEffects.Move);
            return true;
        }

        private static void ReorderFromDrop<T>(ObservableCollection<T> collection, DragEventArgs e) where T : class
        {
            if (collection == null || !e.Data.GetDataPresent(typeof(T)))
                return;

            var source = e.Data.GetData(typeof(T)) as T;
            if (source == null)
                return;

            var target = ResolveItemFromVisual<T>(e.OriginalSource as DependencyObject);
            ReorderCollectionItem(collection, source, target);
        }

        private void CaptureCanvasToTab(WorkspaceDocumentTabState tab)
        {
            if (tab == null || _viewModel == null)
                return;
            tab.CachedIsfBase64 = WorkspaceInkSerialization.StrokesToIsfBase64(WorkspaceInkCanvas.Strokes);
        }

        private void ApplyTabToCanvas(WorkspaceDocumentTabState tab)
        {
            if (tab == null)
                return;
            var strokes = WorkspaceInkSerialization.StrokesFromIsfBase64(tab.CachedIsfBase64);
            _suppressProgrammaticCanvasStrokes = true;
            try
            {
                WorkspaceInkSerialization.ApplyStrokesToCanvas(WorkspaceInkCanvas, strokes);
            }
            finally
            {
                _suppressProgrammaticCanvasStrokes = false;
            }
            double maxX = WorkspaceInkSerialization.GetStrokeBoundsMaxX(strokes);
            double maxY = WorkspaceInkSerialization.GetStrokeBoundsMaxY(strokes);
            _viewModel?.SetContentWidthHintFromLoadedInk(maxX + 32.0);
            _viewModel?.SetContentHeightHintFromLoadedInk(maxY + 32.0);
        }

        private WorkspaceInkDocumentPayload BuildPayloadFromTab(WorkspaceDocumentTabState tab)
        {
            return new WorkspaceInkDocumentPayload
            {
                SchemaVersion = WorkspaceConstants.CurrentSchemaVersion,
                RecognizedTextUtf8 = tab.RecognizedTextUtf8 ?? string.Empty,
                InkIsfBase64 = tab.CachedIsfBase64 ?? string.Empty
            };
        }

        /// <summary>Synchrone Speicherung (z. B. wenn kein async-Kontext ohne Deadlock möglich ist).</summary>
        private void SaveTabToDiskSync(WorkspaceDocumentTabState tab)
        {
            if (tab == null || string.IsNullOrEmpty(tab.FilePath))
                return;
            try
            {
                _storage.WriteDocumentSync(tab.FilePath, BuildPayloadFromTab(tab));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SaveTabToDiskSync: {ex.Message}");
                MessageBox.Show($"Speichern fehlgeschlagen: {ex.Message}", "Workspace", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async Task SaveTabAsync(WorkspaceDocumentTabState tab)
        {
            if (tab == null || string.IsNullOrEmpty(tab.FilePath))
                return;
            try
            {
                await _storage.WriteDocumentAsync(tab.FilePath, BuildPayloadFromTab(tab)).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SaveTabAsync: {ex.Message}");
                MessageBox.Show($"Speichern fehlgeschlagen: {ex.Message}", "Workspace", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async Task OpenOrActivateTabAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !_storage.IsNoteFileInWorkspace(filePath))
                return;
            try
            {
                var existing = _openTabs?.FirstOrDefault(t =>
                    string.Equals(t.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    TabListBox.SelectedItem = existing;
                    return;
                }
                var payload = await _storage.ReadDocumentAsync(filePath).ConfigureAwait(true);
                var tab = new WorkspaceDocumentTabState
                {
                    FilePath = filePath,
                    Title = Path.GetFileName(filePath),
                    CachedIsfBase64 = payload.InkIsfBase64 ?? string.Empty,
                    RecognizedTextUtf8 = payload.RecognizedTextUtf8 ?? string.Empty
                };
                _suspendTabSelectionEvents = true;
                _openTabs.Add(tab);
                TabListBox.SelectedItem = tab;
                _suspendTabSelectionEvents = false;
                await OnTabSelectionChangedCoreAsync(tab, savePrevious: true).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OpenOrActivateTabAsync: {ex.Message}");
                MessageBox.Show($"Datei konnte nicht geöffnet werden: {ex.Message}", "Workspace", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void CloseTabButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if ((sender as Button)?.Tag is WorkspaceDocumentTabState tab)
                    await RemoveTabInternal(tab, saveFirst: true).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CloseTabButton_Click: {ex.Message}");
            }
        }

        private async Task RemoveTabInternal(WorkspaceDocumentTabState tab, bool saveFirst)
        {
            if (tab == null || _openTabs == null)
                return;
            _suspendTabSelectionEvents = true;
            try
            {
                if (saveFirst)
                {
                    if (tab == _activeTab)
                        CaptureCanvasToTab(tab);
                    await SaveTabAsync(tab).ConfigureAwait(true);
                }
                int idx = _openTabs.IndexOf(tab);
                _openTabs.Remove(tab);
                if (_activeTab == tab)
                {
                    _activeTab = null;
                    if (_openTabs.Count > 0)
                    {
                        int newIdx = Math.Min(idx, _openTabs.Count - 1);
                        var nextTab = _openTabs[newIdx];
                        TabListBox.SelectedItem = nextTab;
                        _activeTab = nextTab;
                        ApplyTabToCanvas(nextTab);
                        UpdateSessionActiveFlag();
                    }
                    else
                    {
                        TabListBox.SelectedItem = null;
                        WorkspaceInkCanvas.Strokes.Clear();
                        UpdateSessionActiveFlag();
                        HideEditorChromeIfNoTabs();
                    }
                }
            }
            finally
            {
                _suspendTabSelectionEvents = false;
            }
            SyncWorkspaceCanvasSize();
        }

        private static bool ShouldStartDrag(Point start, Point current)
        {
            return Math.Abs(current.X - start.X) >= SystemParameters.MinimumHorizontalDragDistance ||
                   Math.Abs(current.Y - start.Y) >= SystemParameters.MinimumVerticalDragDistance;
        }

        private void ResetTabDragState()
        {
            _tabDragStartPointSet = false;
            _tabDragCandidate = null;
        }

        private void ResetMainTabDragState()
        {
            _mainTabDragStartPointSet = false;
            _mainTabDragCandidate = null;
        }

        private static T ResolveItemFromVisual<T>(DependencyObject origin) where T : class
        {
            DependencyObject current = origin;
            while (current != null)
            {
                if (current is FrameworkElement element && element.DataContext is T match)
                    return match;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private static void ReorderCollectionItem<T>(ObservableCollection<T> collection, T source, T target)
        {
            if (collection == null || source == null)
                return;

            int sourceIndex = collection.IndexOf(source);
            if (sourceIndex < 0)
                return;

            int targetIndex = target == null ? collection.Count - 1 : collection.IndexOf(target);
            if (targetIndex < 0)
                targetIndex = collection.Count - 1;

            if (sourceIndex == targetIndex)
                return;

            collection.Move(sourceIndex, targetIndex);
        }

        /// <summary>Visuelle Engine: zeichnet Striche auf den <see cref="InkCanvas"/>.</summary>
        private sealed class InkCanvasStrokeSink : IStrokeSink
        {
            private readonly InkCanvas _canvas;

            public InkCanvasStrokeSink(InkCanvas canvas)
            {
                _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            }

            public void AddStroke(System.Collections.Generic.IReadOnlyList<System.Windows.Point> points)
            {
                if (points == null || points.Count == 0)
                    return;

                try
                {
                    var stylusPoints = new StylusPointCollection();
                    foreach (var p in points)
                        stylusPoints.Add(new StylusPoint(p.X, p.Y));

                    var stroke = new WpfStroke(stylusPoints)
                    {
                        DrawingAttributes =
                        {
                            FitToCurve = true,
                            Width = 2.5,
                            Height = 2.5
                        }
                    };

                    if (_canvas.TryFindResource("TextBrush") is SolidColorBrush textBrush)
                        stroke.DrawingAttributes.Color = textBrush.Color;
                    else
                        stroke.DrawingAttributes.Color = Colors.DimGray;

                    stroke.AddPropertyData(PenPipelineStrokePropertyId, true);
                    _canvas.Strokes.Add(stroke);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"InkCanvasStrokeSink.AddStroke: {ex.Message}");
                }
            }
        }

        private sealed class DesktopIconContext
        {
            public DesktopIconContext(WorkspaceDesktopItemInfo item)
            {
                Item = item;
            }

            public WorkspaceDesktopItemInfo Item { get; }

            public TextBlock Label { get; set; }

            public TextBox RenameTextBox { get; set; }

            public Button RenameTriggerButton { get; set; }

            public Grid RenameConfirmGrid { get; set; }

            public bool IsRenameEditing { get; set; }

            public Button DeleteTriggerButton { get; set; }

            public Grid DeleteConfirmGrid { get; set; }
        }

        private sealed class MainViewTabState
        {
            public MainViewTabState(string key, string title, bool canClose)
            {
                Key = key;
                Title = title;
                CanClose = canClose;
            }

            public string Key { get; }

            public string Title { get; }

            public bool CanClose { get; }
        }
    }
}

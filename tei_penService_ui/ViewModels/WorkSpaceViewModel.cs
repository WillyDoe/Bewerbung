using System;
using System.Collections.Generic;
using System.Windows;
using Neosmartpen.Net;
using TeiPenServiceConnectionManager.Utilities;
using tei_penService_ui.Interfaces;
using tei_penService_ui.Models;
using tei_penService_ui.Utilities;

namespace tei_penService_ui.ViewModels
{
    /// <summary>
    /// Workspace: Stiftkoordinaten werden linear von einem NCode-Rechteck (Kalibrierung oder nominales A5)
    /// auf genau eine logische A5-Fläche in Pixeln abgebildet; Seiten-/Notizbuch-Offsets entfallen.
    /// </summary>
    public class WorkspaceViewModel
    {
        private readonly ITeiPenServiceWrapper _teiPenServiceWrapper;
        private readonly List<Dot> _currentStrokeDots = new List<Dot>();

        /// <summary>Optional: Kalibrier-Rechteck aus vier Randstrichen; sonst nominales A5 in NCode-Zellen.</summary>
        private PaperBoundsNCodeCells? _calibrationBoundsNCode;

        private float _offsetX;
        private float _offsetY;

        public event EventHandler LayoutChanged;

        public IStrokeSink StrokeSink { get; set; }

        /// <summary>True, wenn mindestens eine Notiz geöffnet ist und Stift-Input an den Canvas gehen soll.</summary>
        public bool IsInkSessionActive { get; set; }

        /// <summary>
        /// Optional: vor Dot-Verarbeitung z. B. eine neue Notiz öffnen (WorkspaceView).
        /// Bei <c>false</c> wird das Dot-Ereignis verworfen (sparsam einsetzen).
        /// </summary>
        public Func<bool> EnsureInkDocumentForPenInput { get; set; }

        public WorkspaceViewModel()
        {
            _teiPenServiceWrapper = null;
        }

        public WorkspaceViewModel(ITeiPenServiceWrapper teiPenServiceWrapper)
        {
            _teiPenServiceWrapper = teiPenServiceWrapper ?? throw new ArgumentNullException(nameof(teiPenServiceWrapper));
            _teiPenServiceWrapper.DotReceived += OnDotReceived;
        }

        public void SetDisplayOffset(float offsetX, float offsetY)
        {
            _offsetX = offsetX;
            _offsetY = offsetY;
        }

        /// <summary>API-Kompatibilität; die A5-Zuordnung ignoriert den Stride.</summary>
        public void SetPageStrideNCodeCells(float ncodeCells)
        {
        }

        /// <summary>API-Kompatibilität; feste A5-Canvasgröße.</summary>
        public void SetViewportMinWidth(float widthPx)
        {
        }

        /// <summary>API-Kompatibilität; feste A5-Canvasgröße.</summary>
        public void SetViewportMinHeight(float heightPx)
        {
        }

        /// <summary>
        /// Liest die beste Kalibrierungsseite (vier Striche) aus <see cref="WorkspaceConstants.PenStrokeDataDirectoryName"/>.
        /// </summary>
        public void RefreshPaperOriginFromCalibrationFiles(string macAddressOrNull)
        {
            try
            {
                _calibrationBoundsNCode = null;
                if (string.IsNullOrWhiteSpace(macAddressOrNull))
                {
                    RaiseLayoutChanged();
                    return;
                }

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                if (PenCalibrationPageLoader.TryLoadBestCalibrationBounds(baseDir, macAddressOrNull, out PaperBoundsNCodeCells b))
                    _calibrationBoundsNCode = b;

                RaiseLayoutChanged();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WorkspaceViewModel.RefreshPaperOriginFromCalibrationFiles: {ex.Message}");
            }
        }

        /// <summary>Feste logische Breite des InkCanvas (eine A5-Seite).</summary>
        public double GetRequiredCanvasWidth()
        {
            return WorkspacePaperLayout.A5WidthPx;
        }

        /// <summary>Feste logische Höhe des InkCanvas (eine A5-Seite).</summary>
        public double GetRequiredCanvasHeight()
        {
            return WorkspacePaperLayout.A5HeightPx;
        }

        /// <summary>API-Kompatibilität; Canvas bleibt A5.</summary>
        public void SetContentWidthHintFromLoadedInk(double maxXPixel)
        {
        }

        /// <summary>API-Kompatibilität; Canvas bleibt A5.</summary>
        public void SetContentHeightHintFromLoadedInk(double maxYPixel)
        {
        }

        /// <summary>
        /// Nach einem freihändigen Strich auf dem InkCanvas (ohne Stift-Pipeline).
        /// </summary>
        public void NotifyUserInkStrokeCollected()
        {
            if (IsInkSessionActive)
                RaiseLayoutChanged();
        }

        public void Unsubscribe()
        {
            if (_teiPenServiceWrapper != null)
                _teiPenServiceWrapper.DotReceived -= OnDotReceived;
        }

        private static PaperBoundsNCodeCells GetNominalA5BoundsNCode()
        {
            float w = (float)(WorkspacePaperLayout.A5WidthMm / NCodeCoordinateConverter.NCodeCellSizeMm);
            float h = (float)(WorkspacePaperLayout.A5HeightMm / NCodeCoordinateConverter.NCodeCellSizeMm);
            return new PaperBoundsNCodeCells
            {
                MinX = 0f,
                MinY = 0f,
                MaxX = w,
                MaxY = h
            };
        }

        private PaperBoundsNCodeCells GetActivePaperBoundsNCode()
        {
            if (_calibrationBoundsNCode.HasValue)
                return _calibrationBoundsNCode.Value;
            return GetNominalA5BoundsNCode();
        }

        /// <summary>Normierte Papierkoordinate → Pixel auf der logischen A5-Fläche.</summary>
        private Point MapNCodeDotToA5Canvas(float x, float y)
        {
            PaperBoundsNCodeCells b = GetActivePaperBoundsNCode();
            float dx = Math.Max(1e-4f, b.MaxX - b.MinX);
            float dy = Math.Max(1e-4f, b.MaxY - b.MinY);
            double u = (x - b.MinX) / dx;
            double v = (y - b.MinY) / dy;
            double px = u * WorkspacePaperLayout.A5WidthPx + _offsetX;
            double py = v * WorkspacePaperLayout.A5HeightPx + _offsetY;
            return new Point(px, py);
        }

        private void RaiseLayoutChanged()
        {
            LayoutChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnDotReceived(object sender, DotReceivedEventArgs args)
        {
            try
            {
                if (_teiPenServiceWrapper == null || args?.Dot == null || args.Dot.DotType == DotTypes.PEN_ERROR)
                    return;

                if (EnsureInkDocumentForPenInput != null && !EnsureInkDocumentForPenInput())
                    return;

                switch (args.Dot.DotType)
                {
                    case DotTypes.PEN_DOWN:
                        _currentStrokeDots.Clear();
                        _currentStrokeDots.Add(args.Dot);
                        break;
                    case DotTypes.PEN_MOVE:
                        _currentStrokeDots.Add(args.Dot);
                        break;
                    case DotTypes.PEN_UP:
                        _currentStrokeDots.Add(args.Dot);
                        CompleteCurrentStroke();
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WorkspaceViewModel.OnDotReceived: {ex.Message}");
            }
        }

        private void CompleteCurrentStroke()
        {
            try
            {
                if (_currentStrokeDots.Count == 0)
                    return;

                var points = new List<Point>(_currentStrokeDots.Count);
                foreach (Dot dot in _currentStrokeDots)
                    points.Add(MapNCodeDotToA5Canvas(dot.X, dot.Y));

                if (points.Count == 0)
                    return;

                if (points.Count == 1)
                    points.Add(points[0]);

                _teiPenServiceWrapper?.PublishPenStrokeCompleted(points);

                if (IsInkSessionActive && StrokeSink != null)
                {
                    StrokeSink.AddStroke(points);
                    RaiseLayoutChanged();
                }
            }
            finally
            {
                _currentStrokeDots.Clear();
            }
        }
    }
}

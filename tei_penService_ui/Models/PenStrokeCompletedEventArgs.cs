using System;
using System.Collections.Generic;
using System.Windows;

namespace tei_penService_ui.Models
{
    /// <summary>
    /// Ein Stiftstrich wurde aus Dots zu Weltkoordinaten projiziert (Vorarbeit für externe Verarbeitung).
    /// </summary>
    public sealed class PenStrokeCompletedEventArgs : EventArgs
    {
        public PenStrokeCompletedEventArgs(IReadOnlyList<Point> points)
        {
            Points = points ?? Array.Empty<Point>();
        }

        public IReadOnlyList<Point> Points { get; }
    }
}

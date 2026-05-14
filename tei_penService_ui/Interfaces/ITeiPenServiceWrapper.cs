using System;
using System.Collections.Generic;
using System.Windows;
using Neosmartpen.Net;
using TeiPenServiceConnectionManager.Models;
using tei_penService_ui.Models;

namespace tei_penService_ui.Interfaces
{
    /// <summary>
    /// Interface für den TeiPenServiceWrapper.
    /// </summary>
    public interface ITeiPenServiceWrapper : IDisposable
    {
        event EventHandler<DotReceivedEventArgs> DotReceived;

        /// <summary>
        /// Projizierte Strichpunkte (z. B. für externe Handschrift/OCR).
        /// </summary>
        event EventHandler<PenStrokeCompletedEventArgs> PenStrokeCompleted;

        /// <summary>
        /// Erkannter Text von außerhalb der UI (z. B. ausgelagerter Pipeline).
        /// </summary>
        event EventHandler<RecognizedTextEventArgs> RecognizedTextAvailable;

        /// <summary>True, wenn eine Stiftverbindung hergestellt bzw. getrennt wurde.</summary>
        event EventHandler<bool> ConnectionStatusChanged;

        /// <summary>
        /// Meldet einen abgeschlossenen Stiftstrich; wird auf dem UI-Thread verteilt.
        /// </summary>
        void PublishPenStrokeCompleted(IReadOnlyList<Point> points);

        /// <summary>
        /// Meldet erkannten Text; wird auf dem UI-Thread verteilt.
        /// </summary>
        /// <param name="text">Erkannter Text.</param>
        /// <param name="source">Herkunftskennzeichnung (z. B. Dienstname).</param>
        void PublishRecognizedText(string text, string source);

        /// <summary>Liefert Verbindungsinfos zum aktuell verbundenen Stift oder <c>null</c>.</summary>
        PenConnectionInfoModel GetConnectedPenInfo();
    }
}

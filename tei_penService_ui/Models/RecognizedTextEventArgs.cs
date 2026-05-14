using System;

namespace tei_penService_ui.Models
{
    /// <summary>
    /// Freitext-Erkennung von außerhalb der WPF-App (z. B. ausgelagerter OCR-Pipeline).
    /// </summary>
    public sealed class RecognizedTextEventArgs : EventArgs
    {
        public RecognizedTextEventArgs(string text, string source)
        {
            Text = text ?? string.Empty;
            Source = source ?? string.Empty;
        }

        public string Text { get; }

        public string Source { get; }
    }
}

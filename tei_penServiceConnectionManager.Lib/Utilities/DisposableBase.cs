using System;

namespace TeiPenServiceConnectionManager.Utilities
{
    /// <summary>
    /// Basis-Klasse für IDisposable-Implementierungen mit thread-sicherer Dispose-Logik.
    /// </summary>
    public abstract class DisposableBase : IDisposable
    {
        private readonly object _disposeLock = new object();
        private bool _disposed;

        /// <summary>
        /// Prüft threadsafe, ob das Objekt bereits freigegeben wurde.
        /// </summary>
        /// <returns>True, wenn das Objekt bereits freigegeben wurde, false sonst.</returns>
        protected bool IsDisposed()
        {
            lock (_disposeLock)
            {
                return _disposed;
            }
        }

        /// <summary>
        /// Wirft eine ObjectDisposedException, wenn das Objekt bereits freigegeben wurde.
        /// Thread-safe.
        /// </summary>
        protected void ThrowIfDisposed()
        {
            if (IsDisposed())
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        /// <summary>
        /// Implementiert die IDisposable-Schnittstelle und gibt alle Ressourcen frei.
        /// Thread-safe. Verwendet Double-Checked-Locking-Pattern.
        /// </summary>
        public void Dispose()
        {
            bool shouldDispose = false;
            lock (_disposeLock)
            {
                if (_disposed)
                {
                    return;
                }
                _disposed = true;
                shouldDispose = true;
            }

            // Cleanup außerhalb des Locks ausführen (verhindert Deadlocks)
            if (shouldDispose)
            {
                DisposeResources();
            }
        }

        /// <summary>
        /// Überschreibe diese Methode, um spezifische Cleanup-Operationen durchzuführen.
        /// Wird automatisch von Dispose() aufgerufen.
        /// </summary>
        protected abstract void DisposeResources();
    }
}

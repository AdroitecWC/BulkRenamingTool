using pfcls;
using System;
using System.Windows;

namespace RenaimingToolCS.CreoFunctions
{
    public  class CreoSessionManager
    {
        private static readonly object _lock = new object();
        private static CreoSessionManager _instance;

        private IpfcAsyncConnection _asyncConnection;
        private IpfcBaseSession _creoSession;

        // Private constructor
        private CreoSessionManager() { }

        /// <summary>
        /// Singleton Instance
        /// </summary>
        public static CreoSessionManager Instance
        {
            get
            {
                lock (_lock)
                {
                    return _instance ??= new CreoSessionManager();
                }
            }
        }

        /// <summary>
        /// Initialize Creo session: try to connect to running instance, or optionally start a new one
        /// </summary>
        public void InitializeCreoSession()
        {
            try
            {
                var connection = new CCpfcAsyncConnection();

                // Try to connect to an existing session
                try
                {
                    _asyncConnection = connection.Connect(null, null, null, 10);
                }
                catch (Exception connectEx)
                {
                    Console.WriteLine("Could not connect to an existing Creo session: " + connectEx.Message);

                    // Try to start a new Creo session
                    try
                    {
                        _asyncConnection = connection.Start("Creo Parametric", null);
                    }
                    catch (Exception startEx)
                    {
                        MessageBox.Show("Failed to start new Creo session: " + startEx.Message, "Creo Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                // Validate connection
                if (_asyncConnection != null && _asyncConnection.IsRunning())
                {
                    _creoSession = (IpfcBaseSession)_asyncConnection.Session;
                }
                else
                {
                    MessageBox.Show("Could not establish connection with Creo.", "Creo Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unexpected error during Creo session initialization:\n" + ex.Message, "Creo Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Get the active Creo BaseSession
        /// </summary>
        public IpfcBaseSession Session
        {
            get
            {
                if (_creoSession == null)
                {
                    MessageBox.Show("Creo session is not initialized. Call InitializeCreoSession() first.", "Session Not Ready", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                return _creoSession;
            }
        }

        /// <summary>
        /// Dispose of the session and clean up
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (_asyncConnection != null)
                {
                    _asyncConnection.Disconnect(1); // 1 = graceful shutdown
                    _asyncConnection = null;
                }

                _creoSession = null;
                _instance = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error while disposing Creo session: " + ex.Message, "Dispose Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}

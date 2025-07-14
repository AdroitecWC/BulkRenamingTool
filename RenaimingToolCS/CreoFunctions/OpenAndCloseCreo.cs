using pfcls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace RenaimingToolCS.CreoFunctions
{

    internal class OpenAndCloseCreo
    {
        #region Variables
        private string _strWorkingDirectory;
        private string _strProEExePath;
        private bool _blnIsProERunning;
        private Exception _oErrorObject;
        private string _strErrorMessage;
        private IpfcAsyncConnection _iIpfcAsynConnection;
        private CCpfcAsyncConnection _iCcpfcAsynConnection;
        private IpfcBaseSession _session;
        #endregion

        #region Properties
        public string WorkingDirectory
        {
            get { return _strWorkingDirectory; }
        }

        public string ProEExePath
        {
            get { return _strProEExePath; }
        }

        public bool IsProERunning
        {
            get { return _blnIsProERunning; }
            set { _blnIsProERunning = value; }
        }

        public Exception ErrorObject
        {
            get { return _oErrorObject; }
        }

        public string ErrorMessage
        {
            get { return _strErrorMessage; }
        }

        public IpfcAsyncConnection IpfcAsynchronousConnection
        {
            get { return _iIpfcAsynConnection; }
            set { _iIpfcAsynConnection = value; }
        }

        public CCpfcAsyncConnection CcpfcAsynchronousConnection
        {
            get { return _iCcpfcAsynConnection; }
            set { _iCcpfcAsynConnection = value; }
        }

        public IpfcBaseSession Session
        {
            get { return _session; }
            set { _session = value; }
        }
        #endregion

        public bool RunProe(string strWorkingDirectory)
        {
            bool result = true;
            try
            {
                if (!CheckProEOpened(strWorkingDirectory))
                {
                    foreach (var proc in System.Diagnostics.Process.GetProcessesByName("pfclscom"))
                    {
                        if (!proc.HasExited)
                        {
                            proc.Kill();
                        }
                    }

                    string proePath = Environment.GetEnvironmentVariable("PROE_PATH");
                    string myDocsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    string startPath = System.IO.Path.Combine(myDocsPath, "\\");
                    System.Diagnostics.Process.Start(proePath, startPath);

                    System.Threading.Thread.Sleep(5000);

                    _iCcpfcAsynConnection = new CCpfcAsyncConnection();
                    _iIpfcAsynConnection = _iCcpfcAsynConnection.Connect(DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value);
                    Session = (IpfcBaseSession)_iIpfcAsynConnection.Session;
                }

                // Set Working Directory
                if (_iIpfcAsynConnection.IsRunning())
                {
                    _strWorkingDirectory = strWorkingDirectory;
                    Session.ChangeDirectory(strWorkingDirectory);
                }
            }
            catch (Exception oException)
            {
                result = false;
                _oErrorObject = oException;
                _strErrorMessage = "Unable to start process of ProE application" + Environment.NewLine +
                                   "Working directory given " + strWorkingDirectory + Environment.NewLine + Environment.NewLine +
                                   "System generated error:-" + Environment.NewLine + oException.Message;
                MessageBox.Show(_strErrorMessage);
            }

            return result;
        }
        private bool CheckProEOpened(string strWorkingDirectory)
        {
            string errMsg = "";

            if (!(System.Diagnostics.Process.GetProcessesByName("nmsd").Length > 0))
            {
                errMsg = "Name service is not running on the system.";
            }

            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PRO_COMM_MSG_EXE")))
            {
                if (string.IsNullOrEmpty(errMsg))
                {
                    errMsg = "Environment variable 'PRO_COMM_MSG_EXE' has not been set.";
                }
                else
                {
                    errMsg += Environment.NewLine + "Environment variable 'PRO_COMM_MSG_EXE' has not been set.";
                }
            }

            if (Environment.GetEnvironmentVariable("PRO_DIRECTORY") == null)
            {
                if (string.IsNullOrEmpty(errMsg))
                {
                    errMsg = "Environment variable 'PRO_DIRECTORY' has not been set.";
                }
                else
                {
                    errMsg += Environment.NewLine + "Environment variable 'PRO_DIRECTORY' has not been set.";
                }
            }

            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PROE_PATH")))
            {
                if (string.IsNullOrEmpty(errMsg))
                {
                    errMsg = "Environment variable 'PROE_PATH' has not been set.";
                }
                else
                {
                    errMsg += Environment.NewLine + "Environment variable 'PROE_PATH' has not been set.";
                }
            }

            bool result = true;
            try
            {
                _iCcpfcAsynConnection = new CCpfcAsyncConnection();

                if (_iIpfcAsynConnection == null)
                {
                    _iIpfcAsynConnection = CcpfcAsynchronousConnection.Start(
                        Environment.GetEnvironmentVariable("PROE_PATH"),
                        strWorkingDirectory);
                }

                // ✅ Assign the session properly before using
                Session = (IpfcBaseSession)_iIpfcAsynConnection.Session;

                if (_iIpfcAsynConnection.IsRunning())
                {
                    _strWorkingDirectory = strWorkingDirectory;
                    Session.ChangeDirectory(strWorkingDirectory);
                }
            



                // Set working directory
                _strWorkingDirectory = strWorkingDirectory;
                _blnIsProERunning = true;

                System.Threading.Thread.Sleep(1000);
            }
            catch (Exception oException)
            {
                CloseProe();
                KillCreO();
                _oErrorObject = oException;
                result = false;
                _strErrorMessage = "Unable to Connect Active ProE" + Environment.NewLine +
                                   "Application is Trying to Open ProE" + Environment.NewLine +
                                   "System generated error:-" + Environment.NewLine + oException.Message;
                MessageBox.Show(_strErrorMessage);
            }

            return result;
        }

        public bool CloseProe()
        {
            bool result = true;
            try
            {
                // Check for the object IpfcAsynconnection and if it is running
                if (_iIpfcAsynConnection != null)
                {
                    if (_iIpfcAsynConnection.IsRunning())
                    {
                        _iIpfcAsynConnection.End();
                    }

                    System.Runtime.InteropServices.Marshal.ReleaseComObject(_iIpfcAsynConnection);
                    _iIpfcAsynConnection = null;
                    GC.Collect();
                }
            }
            catch (Exception oException)
            {
                result = false;
                _oErrorObject = oException;
                _strErrorMessage = "Unable to Close ProE" + Environment.NewLine +
                                   "System generated error:-" + Environment.NewLine + oException.Message;
                MessageBox.Show(_strErrorMessage);
            }
            return result;
        }

        public void KillCreO()
        {
            try
            {
                foreach (var proc in System.Diagnostics.Process.GetProcessesByName("xtop"))
                {
                    if (!proc.HasExited)
                    {
                        if (!proc.Responding)
                        {
                            proc.Kill();
                        }
                        proc.Kill();
                    }
                }

                foreach (var proc in System.Diagnostics.Process.GetProcessesByName("nmsd"))
                {
                    if (!proc.HasExited)
                    {
                        proc.Kill();
                    }
                }
            }
            catch (Exception)
            {
                // Suppress any exceptions
            }
        }

    }
}

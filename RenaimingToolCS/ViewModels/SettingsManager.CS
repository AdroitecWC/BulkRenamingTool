namespace RenaimingToolCS.Helpers
{
    public class SettingsManager
    {
        private static SettingsManager _instance;

        public string CreoPath { get; set; }
        public string ProDirectory { get; set; }
        public string ProCommMsgExe { get; set; }
        public string VbApiRegisterBatPath { get; set; }
        public string CommonFilesFolder { get; set; }
        public string WorkingDirectory { get; set; } // Added missing property

        private SettingsManager()
        {
            CreoPath = string.Empty;
            ProDirectory = string.Empty;
            ProCommMsgExe = string.Empty;
            VbApiRegisterBatPath = string.Empty;
            CommonFilesFolder = string.Empty;
            WorkingDirectory = string.Empty;
        }

        public static SettingsManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new SettingsManager();
                }
                return _instance;
            }
        }

        // TODO: Add Load/Save methods if you want persistence
    }
}

using System;
using System.Linq;

namespace Barebones.MasterServer
{
    public class MsfArgs
    {
        private readonly string[] _args;

        public MsfArgNames Names;

        public MsfArgs()
        {
            _args = Environment.GetCommandLineArgs();

            // Android fix
            if (_args == null)
                _args = new string[0];

            Names = new MsfArgNames();

            StartMaster = IsProvided(Names.StartMaster);
            MasterPort = ExtractValueInt(Names.MasterPort, 5000);
            MasterIp = ExtractValue(Names.MasterIp);
            MachineIp = ExtractValue(Names.MachineIp);
            DestroyUi = IsProvided(Names.DestroyUi);

            SpawnId = ExtractValueInt(Names.SpawnId, -1);
            AssignedPort = ExtractValueInt(Names.AssignedPort, -1);
            SpawnCode = ExtractValue(Names.SpawnCode);
            ExecutablePath = ExtractValue(Names.ExecutablePath);
            SpawnInBatchmode = IsProvided(Names.SpawnInBatchmode);
            MaxProcesses = ExtractValueInt(Names.MaxProcesses, 0);

            LoadScene = ExtractValue(Names.LoadScene);

            DbConnectionString = ExtractValue(Names.DbConnectionString);

            LobbyId = ExtractValueInt(Names.LobbyId);
            WebGl = IsProvided(Names.WebGl);
        }

        #region Arguments

        public bool StartMaster { get; private set; }
        public int MasterPort { get; private set; }
        public string MasterIp { get; private set; }
        public string MachineIp { get; private set; }
        public bool DestroyUi { get; private set; }

        public int SpawnId { get; private set; }
        public int AssignedPort { get; private set; }
        public string SpawnCode { get; private set; }
        public string ExecutablePath { get; private set; }
        public bool SpawnInBatchmode { get; private set; }
        public int MaxProcesses { get; private set; }

        public string LoadScene { get; private set; }

        public string DbConnectionString { get; private set; }

        public int LobbyId { get; private set; }
        public bool WebGl { get; private set; }

        #endregion

        #region Helper methods

        /// <summary>
        ///     Extracts a value for command line arguments provided
        /// </summary>
        /// <param name="argName"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public string ExtractValue(string argName, string defaultValue = null)
        {
            if (!_args.Contains(argName))
                return defaultValue;

            var index = _args.ToList().FindIndex(0, a => a.Equals(argName));
            return _args[index + 1];
        }

        public int ExtractValueInt(string argName, int defaultValue = -1)
        {
            var number = ExtractValue(argName, defaultValue.ToString());
            return Convert.ToInt32(number);
        }

        public bool IsProvided(string argName)
        {
            return _args.Contains(argName);
        }

        #endregion

        public class MsfArgNames
        {
            public string StartMaster { get { return "-msfStartMaster"; } }
            public string MasterPort { get { return "-msfMasterPort"; } }
            public string MasterIp { get { return "-msfMasterIp"; } }

            public string StartSpawner { get { return "-msfStartSpawner"; } }

            public string SpawnId { get { return "-msfSpawnId"; } }
            public string SpawnCode { get { return "-msfSpawnCode"; } }
            public string AssignedPort { get { return "-msfAssignedPort"; } }
            public string LoadScene { get { return "-msfLoadScene"; } }
            public string MachineIp { get { return "-msfMachineIp"; } }
            public string ExecutablePath { get { return "-msfExe"; } }
            public string DbConnectionString { get { return "-msfDbConnectionString"; } }
            public string LobbyId { get { return "-msfLobbyId"; } }
            public string SpawnInBatchmode { get { return "-msfBatcmode"; } }
            public string MaxProcesses { get { return "-msfMaxProcesses"; } }
            public string DestroyUi { get { return "-msfDestroyUi"; } }
            public string WebGl { get { return "-msfWebgl"; } }
        }
    }
}
using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using AppBar.Core.Models;

namespace AppBar.Core.Services
{
    public class JsonStateStore
    {
        private readonly object _syncRoot = new object();
        private readonly JavaScriptSerializer _serializer;

        public JsonStateStore(string dataDirectory)
        {
            if (string.IsNullOrWhiteSpace(dataDirectory))
            {
                throw new ArgumentException("Data directory is required.", "dataDirectory");
            }

            DataDirectory = dataDirectory;
            StateFilePath = Path.Combine(dataDirectory, "state.json");
            _serializer = new JavaScriptSerializer();
            _serializer.MaxJsonLength = 4 * 1024 * 1024;
        }

        public string DataDirectory { get; private set; }
        public string StateFilePath { get; private set; }
        public string LastLoadWarning { get; private set; }

        public bool StateExists
        {
            get { return File.Exists(StateFilePath); }
        }

        public LauncherState Load()
        {
            lock (_syncRoot)
            {
                LastLoadWarning = string.Empty;
                if (!File.Exists(StateFilePath))
                {
                    return LauncherState.CreateDefault();
                }

                try
                {
                    string json = File.ReadAllText(StateFilePath, Encoding.UTF8);
                    LauncherState state = _serializer.Deserialize<LauncherState>(json);
                    if (state == null)
                    {
                        throw new InvalidDataException("State file is empty.");
                    }

                    state.Normalize();
                    return state;
                }
                catch (Exception)
                {
                    PreserveCorruptState();
                    LastLoadWarning =
                        "状态文件无法读取，已在原目录保留损坏文件备份，并以空配置启动。";
                    return LauncherState.CreateDefault();
                }
            }
        }

        public void Save(LauncherState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException("state");
            }

            lock (_syncRoot)
            {
                state.Normalize();
                Directory.CreateDirectory(DataDirectory);

                string temporaryPath = StateFilePath + ".tmp";
                string json = _serializer.Serialize(state);
                File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));

                if (File.Exists(StateFilePath))
                {
                    string backupPath = StateFilePath + ".bak";
                    File.Replace(temporaryPath, StateFilePath, backupPath, true);
                }
                else
                {
                    File.Move(temporaryPath, StateFilePath);
                }
            }
        }

        private void PreserveCorruptState()
        {
            try
            {
                string suffix = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
                string backupPath = StateFilePath + ".corrupt-" + suffix + ".json";
                File.Copy(StateFilePath, backupPath, false);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}


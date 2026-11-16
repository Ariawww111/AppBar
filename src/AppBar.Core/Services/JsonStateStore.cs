using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using AppBar.Core.Models;

namespace AppBar.Core.Services
{
    public class JsonStateStore
    {
        private const string BackupSuffix = ".bak";
        private const string LastGoodSuffix = ".last-good";
        private const string TemporarySuffix = ".tmp";
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
        public string LastRecoveryPath { get; private set; }
        public string LastLoadDiagnostic { get; private set; }

        public bool StateExists
        {
            get { return File.Exists(StateFilePath); }
        }

        public LauncherState Load()
        {
            lock (_syncRoot)
            {
                LastLoadWarning = string.Empty;
                LastRecoveryPath = string.Empty;
                LastLoadDiagnostic = string.Empty;
                AppendLoadDiagnostic(
                    "primaryExists=" + File.Exists(StateFilePath)
                    + "; path=" + StateFilePath);
                if (!File.Exists(StateFilePath))
                {
                    LauncherState recovered = TryLoadRecoverableState();
                    if (recovered != null)
                    {
                        SetRecoveredState(recovered, LastRecoveryPath);
                        return recovered;
                    }

                    return LauncherState.CreateDefault();
                }

                string primaryError;
                LauncherState primary = TryLoadFile(StateFilePath, out primaryError);
                if (primary != null)
                {
                    if (primary.Apps.Count == 0 && !primary.EmptyStateConfirmed)
                    {
                        LauncherState recovered = TryLoadRecoverableState();
                        if (recovered != null && recovered.Apps.Count > 0)
                        {
                            SetRecoveredState(recovered, LastRecoveryPath);
                            return recovered;
                        }
                    }

                    return primary;
                }

                PreserveCorruptState();
                LauncherState backup = TryLoadRecoverableState();
                if (backup != null)
                {
                    SetRecoveredState(backup, LastRecoveryPath);
                    return backup;
                }

                LastLoadWarning =
                    "状态文件无法读取，已在原目录保留损坏文件备份，并以空配置启动。"
                    + (string.IsNullOrWhiteSpace(primaryError)
                        ? string.Empty
                        : "\n原因：" + primaryError);
                return LauncherState.CreateDefault();
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

                if (state.Apps.Count == 0 &&
                    !state.EmptyStateConfirmed &&
                    HasExistingAppState())
                {
                    throw new InvalidOperationException(
                        "检测到应用列表意外为空，已拒绝覆盖现有配置。");
                }

                string temporaryPath = StateFilePath + TemporarySuffix;
                string json = _serializer.Serialize(state);
                File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));

                string validationError;
                LauncherState validated = TryLoadFile(temporaryPath, out validationError);
                if (validated == null ||
                    validated.Apps.Count != state.Apps.Count ||
                    !AppsMatch(state.Apps, validated.Apps))
                {
                    TryDeleteFile(temporaryPath);
                    throw new InvalidDataException(
                        "新配置写入后校验失败，已取消保存。"
                        + (string.IsNullOrWhiteSpace(validationError)
                            ? string.Empty
                            : " " + validationError));
                }

                if (File.Exists(StateFilePath))
                {
                    string backupPath = StateFilePath + BackupSuffix;
                    File.Replace(temporaryPath, StateFilePath, backupPath, true);
                }
                else
                {
                    File.Move(temporaryPath, StateFilePath);
                }

                if (state.Apps.Count > 0)
                {
                    File.Copy(StateFilePath, StateFilePath + LastGoodSuffix, true);
                }
            }
        }

        private LauncherState TryLoadRecoverableState()
        {
            foreach (string path in ListRecoveryCandidates())
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                string error;
                LauncherState recovered = TryLoadFile(path, out error);
                if (recovered != null && recovered.Apps.Count > 0)
                {
                    LastRecoveryPath = path;
                    return recovered;
                }
            }

            return null;
        }

        private IEnumerable<string> ListRecoveryCandidates()
        {
            yield return StateFilePath + BackupSuffix;
            yield return StateFilePath + LastGoodSuffix;
        }

        private LauncherState TryLoadFile(string path, out string error)
        {
            error = string.Empty;
            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                object document = _serializer.DeserializeObject(json);
                IDictionary documentMap = document as IDictionary;
                IList documentApps = documentMap == null
                    ? null
                    : documentMap["Apps"] as IList;
                AppendLoadDiagnostic(
                    "candidate=" + path
                    + "; bytes=" + Encoding.UTF8.GetByteCount(json)
                    + "; jsonApps=" + (documentApps == null ? -1 : documentApps.Count));

                LauncherState state = _serializer.Deserialize<LauncherState>(json);
                if (state == null)
                {
                    throw new InvalidDataException("State file is empty.");
                }

                AppendLoadDiagnostic(
                    "deserializedApps=" + (state.Apps == null ? -1 : state.Apps.Count)
                    + "; schema=" + state.SchemaVersion);
                state.Normalize();
                AppendLoadDiagnostic(
                    "normalizedApps=" + state.Apps.Count
                    + "; emptyConfirmed=" + state.EmptyStateConfirmed);
                return state;
            }
            catch (Exception ex)
            {
                AppendLoadDiagnostic(
                    "candidateFailed=" + path + "; error=" + ex.Message);
                error = ex.Message;
                return null;
            }
        }

        private void AppendLoadDiagnostic(string message)
        {
            if (string.IsNullOrWhiteSpace(LastLoadDiagnostic))
            {
                LastLoadDiagnostic = message;
            }
            else
            {
                LastLoadDiagnostic += Environment.NewLine + message;
            }
        }

        private bool HasExistingAppState()
        {
            string error;
            LauncherState current = TryLoadFile(StateFilePath, out error);
            if (current != null && current.Apps.Count > 0)
            {
                return true;
            }

            foreach (string path in ListRecoveryCandidates())
            {
                LauncherState candidate = TryLoadFile(path, out error);
                if (candidate != null && candidate.Apps.Count > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void SetRecoveredState(LauncherState state, string recoveryPath)
        {
            LastRecoveryPath = recoveryPath ?? string.Empty;
            LastLoadWarning =
                "应用列表已从备份恢复，共 " + state.Apps.Count + " 个应用。";
        }

        private static bool AppsMatch(
            IList<AppShortcut> expected,
            IList<AppShortcut> actual)
        {
            if (expected.Count != actual.Count)
            {
                return false;
            }

            for (int index = 0; index < expected.Count; index++)
            {
                if (!string.Equals(
                        expected[index].Id,
                        actual[index].Id,
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(
                        expected[index].DisplayName,
                        actual[index].DisplayName,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        expected[index].LaunchPath,
                        actual[index].LaunchPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
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


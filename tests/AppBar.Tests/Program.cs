using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using AppBar.Core.Models;
using AppBar.Core.Services;
using AppBar.Core.Utilities;

namespace AppBar.Tests
{
    internal static class Program
    {
        private static int _passed;
        private static int _failed;

        private static int Main()
        {
            Console.WriteLine("AppBar automated tests");
            Console.WriteLine("---------------------");

            Run("window geometry clamps a bubble to the work area", TestBubbleClamping);
            Run("panel size stays close to one eighth of the screen", TestPanelSize);
            Run("panel flips above a bottom bubble", TestPanelFlipsAbove);
            Run("panel clamping supports a monitor with negative coordinates", TestNegativeMonitor);
            Run("supported extensions are filtered", TestSupportedExtensions);
            Run("target paths are normalized for duplicate detection", TestTargetNormalization);
            Run("ordered list move preserves all items", TestOrderedListMove);
            Run("JSON state survives an atomic save and reload", TestStateRoundTrip);
            Run("corrupt JSON is preserved and reported", TestCorruptState);
            Run("accidental empty state recovers from last good backup", TestEmptyStateRecovery);
            Run("empty save cannot overwrite an existing app list", TestRejectsEmptyOverwrite);
            Run("intentional empty state remains empty", TestIntentionalEmptyState);
            Run("desktop shortcut resolves its target", TestShortcutResolution);
            Run("shortcut target falls back to its existing working directory", TestRedirectedShortcutTarget);
            Run("missing applications fail without throwing", TestMissingLaunch);
            Run("an existing application can be launched", TestExistingLaunch);
            Run("startup registration can be enabled and disabled", TestStartupToggle);

            Console.WriteLine();
            Console.WriteLine("Passed: " + _passed + ", Failed: " + _failed);
            return _failed == 0 ? 0 : 1;
        }

        private static void TestBubbleClamping()
        {
            ScreenBounds work = new ScreenBounds(0, 0, 1920, 1040);
            SizedPlacement position =
                WindowGeometry.ClampBubble(-100, 1500, 56, 56, work);

            AssertEqual(8, position.X, "left edge");
            AssertEqual(976, position.Y, "bottom edge");
            AssertEqual(56, position.Width, "bubble width");
        }

        private static void TestPanelSize()
        {
            ScreenBounds work = new ScreenBounds(0, 0, 1920, 1040);
            SizedPlacement panel = WindowGeometry.CalculatePanelSize(work, 1.0);
            double areaRatio =
                (double)(panel.Width * panel.Height) / (work.Width * work.Height);

            AssertTrue(panel.Width >= 560 && panel.Width <= 800, "panel width bounds");
            AssertTrue(panel.Height >= 280 && panel.Height <= 360, "panel height bounds");
            AssertTrue(areaRatio > 0.10 && areaRatio < 0.18, "panel area ratio");
        }

        private static void TestPanelFlipsAbove()
        {
            ScreenBounds work = new ScreenBounds(0, 0, 1920, 1040);
            ScreenBounds bubble = new ScreenBounds(950, 960, 56, 56);
            SizedPlacement panel = WindowGeometry.PlacePanel(work, bubble, 560, 300, 1.0);

            AssertTrue(panel.Y < bubble.Y, "panel should flip above");
            AssertTrue(panel.Y >= work.Y + 12, "panel should remain inside work area");
        }

        private static void TestNegativeMonitor()
        {
            ScreenBounds work = new ScreenBounds(-1920, 0, 1920, 1040);
            ScreenBounds bubble = new ScreenBounds(-100, 40, 56, 56);
            SizedPlacement panel = WindowGeometry.PlacePanel(work, bubble, 560, 300, 1.0);

            AssertTrue(panel.X >= work.X + 12, "negative monitor left edge");
            AssertTrue(panel.X + panel.Width <= work.Right - 12, "negative monitor right edge");
        }

        private static void TestSupportedExtensions()
        {
            ShortcutResolver resolver = new ShortcutResolver();
            AssertTrue(resolver.IsSupported("Sample.LNK"), "uppercase lnk");
            AssertTrue(resolver.IsSupported("Sample.exe"), "exe");
            AssertFalse(resolver.IsSupported("Sample.bat"), "bat must be rejected");
            AssertFalse(resolver.IsSupported("Sample.txt"), "text must be rejected");
        }

        private static void TestTargetNormalization()
        {
            string executable = Path.Combine(Environment.SystemDirectory, "notepad.exe");
            AppShortcut shortcut = new AppShortcut { LaunchPath = executable + Path.DirectorySeparatorChar };
            List<AppShortcut> list = new List<AppShortcut> { shortcut };

            AssertEqual(
                ShortcutResolver.NormalizeTargetPath(executable),
                ShortcutResolver.NormalizeTargetPath(shortcut.LaunchPath),
                "trailing separators");
            AssertTrue(ShortcutResolver.ContainsTarget(list, executable), "duplicate lookup");
        }

        private static void TestOrderedListMove()
        {
            List<string> values = new List<string> { "a", "b", "c", "d" };
            bool moved = OrderedList.Move(values, 1, 3);
            AssertTrue(moved, "move result");
            AssertEqual("a,c,d,b", string.Join(",", values.ToArray()), "moved sequence");
        }

        private static void TestStateRoundTrip()
        {
            string directory = CreateTemporaryDirectory("roundtrip");
            try
            {
                JsonStateStore store = new JsonStateStore(directory);
                LauncherState state = LauncherState.CreateDefault();
                state.BubbleX = 120;
                state.BubbleY = 80;
                state.BubblePositionInitialized = true;
                state.StartupConfigured = true;
                state.Apps.Add(new AppShortcut
                {
                    DisplayName = "Notepad",
                    LaunchPath = Path.Combine(Environment.SystemDirectory, "notepad.exe")
                });

                store.Save(state);
                LauncherState loaded = store.Load();
                AssertEqual(120, (int)loaded.BubbleX, "bubble x");
                AssertEqual(80, (int)loaded.BubbleY, "bubble y");
                AssertEqual(1, loaded.Apps.Count, "app count");
                AssertTrue(File.Exists(store.StateFilePath), "state file exists");

                store.Save(loaded);
                AssertTrue(File.Exists(store.StateFilePath + ".bak"), "previous state backup");
            }
            finally
            {
                TryDeleteDirectory(directory);
            }
        }

        private static void TestCorruptState()
        {
            string directory = CreateTemporaryDirectory("corrupt");
            try
            {
                JsonStateStore store = new JsonStateStore(directory);
                File.WriteAllText(store.StateFilePath, "{not-json", System.Text.Encoding.UTF8);

                LauncherState state = store.Load();
                AssertEqual(0, state.Apps.Count, "fallback state");
                AssertTrue(
                    !string.IsNullOrWhiteSpace(store.LastLoadWarning),
                    "warning is exposed");
                AssertTrue(
                    Directory.GetFiles(directory, "state.json.corrupt-*.json").Length == 1,
                    "corrupt source is preserved");
            }
            finally
            {
                TryDeleteDirectory(directory);
            }
        }

        private static void TestEmptyStateRecovery()
        {
            string directory = CreateTemporaryDirectory("recovery");
            try
            {
                JsonStateStore store = new JsonStateStore(directory);
                LauncherState state = CreateStateWithNotepad();
                store.Save(state);

                File.WriteAllText(
                    store.StateFilePath,
                    "{\"SchemaVersion\":2,\"BubbleX\":24,\"BubbleY\":24,"
                    + "\"BubblePositionInitialized\":true,\"StartupConfigured\":true,"
                    + "\"EmptyStateConfirmed\":false,\"Apps\":[]}",
                    System.Text.Encoding.UTF8);

                LauncherState recovered = store.Load();
                AssertEqual(1, recovered.Apps.Count, "recovered app count");
                AssertTrue(
                    !string.IsNullOrWhiteSpace(store.LastLoadWarning),
                    "recovery warning is exposed");
                AssertTrue(
                    File.Exists(store.StateFilePath + ".last-good"),
                    "last good backup exists");
            }
            finally
            {
                TryDeleteDirectory(directory);
            }
        }

        private static void TestRejectsEmptyOverwrite()
        {
            string directory = CreateTemporaryDirectory("reject-empty");
            try
            {
                JsonStateStore store = new JsonStateStore(directory);
                LauncherState state = CreateStateWithNotepad();
                store.Save(state);

                state.Apps.Clear();
                state.EmptyStateConfirmed = false;
                AssertThrows<InvalidOperationException>(
                    delegate { store.Save(state); },
                    "empty overwrite should be rejected");
            }
            finally
            {
                TryDeleteDirectory(directory);
            }
        }

        private static void TestIntentionalEmptyState()
        {
            string directory = CreateTemporaryDirectory("intentional-empty");
            try
            {
                JsonStateStore store = new JsonStateStore(directory);
                LauncherState state = CreateStateWithNotepad();
                store.Save(state);

                state.Apps.Clear();
                state.EmptyStateConfirmed = true;
                store.Save(state);

                LauncherState loaded = store.Load();
                AssertEqual(0, loaded.Apps.Count, "confirmed empty state");
                AssertTrue(loaded.EmptyStateConfirmed, "empty confirmation persists");
            }
            finally
            {
                TryDeleteDirectory(directory);
            }
        }

        private static void TestShortcutResolution()
        {
            string directory = CreateTemporaryDirectory("shortcut");
            try
            {
                string linkPath = Path.Combine(directory, "Notepad Test.lnk");
                string target = Path.Combine(Environment.SystemDirectory, "notepad.exe");
                CreateShortcut(linkPath, target, Environment.SystemDirectory, "--test-argument");

                ShortcutResolutionResult result =
                    new ShortcutResolver().Resolve(linkPath);
                AssertTrue(result.Success, "shortcut resolution succeeds");
                AssertTrue(
                    string.Equals(
                        ShortcutResolver.NormalizeTargetPath(target),
                        ShortcutResolver.NormalizeTargetPath(result.Shortcut.LaunchPath),
                        StringComparison.OrdinalIgnoreCase),
                    "resolved target");
                AssertEqual("--test-argument", result.Shortcut.Arguments, "arguments");
            }
            finally
            {
                TryDeleteDirectory(directory);
            }
        }

        private static void TestRedirectedShortcutTarget()
        {
            string directory = CreateTemporaryDirectory("redirected-shortcut");
            try
            {
                string workingDirectory = Path.Combine(directory, "Program Files", "Sample App");
                string executable = Path.Combine(workingDirectory, "SampleApp.exe");
                Directory.CreateDirectory(workingDirectory);
                File.WriteAllText(executable, "test executable placeholder");

                string redirectedTarget =
                    Path.Combine(directory, "Program Files (x86)", "Sample App", "SampleApp.exe");
                string linkPath = Path.Combine(directory, "Sample App.lnk");
                CreateShortcut(linkPath, redirectedTarget, workingDirectory, string.Empty);

                ShortcutResolutionResult result =
                    new ShortcutResolver().Resolve(linkPath);
                AssertTrue(result.Success, "redirected shortcut resolution succeeds");
                AssertEqual(
                    ShortcutResolver.NormalizeTargetPath(executable),
                    ShortcutResolver.NormalizeTargetPath(result.Shortcut.LaunchPath),
                    "working-directory fallback target");
                AssertEqual(
                    workingDirectory,
                    result.Shortcut.WorkingDirectory,
                    "working-directory fallback keeps working directory");
            }
            finally
            {
                TryDeleteDirectory(directory);
            }
        }

        private static void TestMissingLaunch()
        {
            AppShortcut shortcut = new AppShortcut
            {
                DisplayName = "Missing",
                LaunchPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".exe")
            };
            AppLaunchResult result = new AppLauncher().Launch(shortcut);
            AssertFalse(result.Success, "missing app should fail");
            AssertTrue(
                result.ErrorMessage.Contains("找不到"),
                "missing app error should be explicit");
        }

        private static void TestExistingLaunch()
        {
            string commandProcessor = Path.Combine(Environment.SystemDirectory, "cmd.exe");
            AppLaunchResult result = new AppLauncher().Launch(new AppShortcut
            {
                DisplayName = "Command processor",
                LaunchPath = commandProcessor,
                Arguments = "/c exit 0",
                WorkingDirectory = Environment.SystemDirectory
            });
            AssertTrue(result.Success, "existing executable should launch");
        }

        private static void TestStartupToggle()
        {
            FakeRegistryValueStore registry = new FakeRegistryValueStore();
            FakeStartupShortcutStore shortcut = new FakeStartupShortcutStore();
            string executable = Path.Combine(Path.GetTempPath(), "AppBar.Startup.Test.exe");
            StartupService service = new StartupService(executable, registry, shortcut);
            service.SetEnabled(true);
            AssertTrue(service.IsEnabled(), "startup enabled");
            AssertEqual("\"" + executable + "\"", registry.StoredValue, "startup command");
            AssertEqual(executable, shortcut.ExecutablePath, "startup shortcut target");
            service.SetEnabled(false);
            AssertFalse(service.IsEnabled(), "startup disabled");
            AssertTrue(shortcut.ExecutablePath == null, "startup shortcut removed");
        }

        private static void CreateShortcut(
            string shortcutPath,
            string targetPath,
            string workingDirectory,
            string arguments)
        {
            object shell = null;
            object shortcut = null;
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                AssertTrue(shellType != null, "WScript.Shell is available");
                shell = Activator.CreateInstance(shellType);
                shortcut = shellType.InvokeMember(
                    "CreateShortcut",
                    BindingFlags.InvokeMethod,
                    null,
                    shell,
                    new object[] { shortcutPath });
                Type shortcutType = shortcut.GetType();
                shortcutType.InvokeMember(
                    "TargetPath",
                    BindingFlags.SetProperty,
                    null,
                    shortcut,
                    new object[] { targetPath });
                shortcutType.InvokeMember(
                    "WorkingDirectory",
                    BindingFlags.SetProperty,
                    null,
                    shortcut,
                    new object[] { workingDirectory });
                shortcutType.InvokeMember(
                    "Arguments",
                    BindingFlags.SetProperty,
                    null,
                    shortcut,
                    new object[] { arguments });
                shortcutType.InvokeMember(
                    "Save",
                    BindingFlags.InvokeMethod,
                    null,
                    shortcut,
                    null);
            }
            finally
            {
                ReleaseComObject(shortcut);
                ReleaseComObject(shell);
            }
        }

        private static void ReleaseComObject(object value)
        {
            if (value != null && Marshal.IsComObject(value))
            {
                Marshal.FinalReleaseComObject(value);
            }
        }

        private static string CreateTemporaryDirectory(string suffix)
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "AppBar.Tests." + suffix + "." + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static LauncherState CreateStateWithNotepad()
        {
            LauncherState state = LauncherState.CreateDefault();
            state.Apps.Add(new AppShortcut
            {
                DisplayName = "Notepad",
                LaunchPath = Path.Combine(Environment.SystemDirectory, "notepad.exe")
            });
            return state;
        }

        private static void TryDeleteDirectory(string directory)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void Run(string name, Action test)
        {
            try
            {
                test();
                _passed++;
                Console.WriteLine("[PASS] " + name);
            }
            catch (Exception ex)
            {
                _failed++;
                Console.WriteLine("[FAIL] " + name + ": " + ex.Message);
            }
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void AssertFalse(bool condition, string message)
        {
            AssertTrue(!condition, message);
        }

        private static void AssertThrows<TException>(Action action, string message)
            where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }

            throw new InvalidOperationException(
                message + " (expected exception: " + typeof(TException).Name + ")");
        }

        private static void AssertEqual<T>(T expected, T actual, string message)
        {
            if (!object.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    message + " (expected: " + expected + ", actual: " + actual + ")");
            }
        }

        private sealed class FakeRegistryValueStore : IRegistryValueStore
        {
            public string StoredValue { get; private set; }

            public string GetValue(string keyPath, string valueName)
            {
                return StoredValue;
            }

            public void SetValue(string keyPath, string valueName, string value)
            {
                StoredValue = value;
            }

            public void DeleteValue(string keyPath, string valueName)
            {
                StoredValue = null;
            }
        }

        private sealed class FakeStartupShortcutStore : IStartupShortcutStore
        {
            public string ExecutablePath { get; private set; }

            public bool IsCurrent(string executablePath)
            {
                return string.Equals(
                    ExecutablePath,
                    executablePath,
                    StringComparison.OrdinalIgnoreCase);
            }

            public void SetCurrent(string executablePath)
            {
                ExecutablePath = executablePath;
            }

            public void Remove()
            {
                ExecutablePath = null;
            }
        }
    }
}

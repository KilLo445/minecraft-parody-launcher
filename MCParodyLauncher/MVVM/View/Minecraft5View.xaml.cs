﻿using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Microsoft.Toolkit.Uwp.Notifications;
using WinForms = System.Windows.Forms;
using System.Threading.Tasks;
using Wsh = IWshRuntimeLibrary;
using System.Windows.Input;

namespace MCParodyLauncher.MVVM.View
{
    enum MC5Status
    {
        ready,
        noInstall,
        downloading,
        checkUpdate,
        update,
        updating,
        installing,
        error
    }

    public partial class Minecraft5View : UserControl
    {
        public string GameName = "Minecraft 5";
        public string GameNameS = "MC5";
        public string GameProcess = "MC5";

        private string gameLink = "https://www.dropbox.com/s/6b06sm6ttwuljqs/mc5.zip?dl=1";
        private string gameStore = "https://decentgamestudio.itch.io/minecraft-5";
        private string verLink = "https://raw.githubusercontent.com/KilLo445/MCParodyLauncher/master/Versions/Games/MC5/version.txt";
        private string sizeLink = "https://raw.githubusercontent.com/KilLo445/MCParodyLauncher/master/Versions/Games/MC5/size.txt";

        // Paths
        private string rootPath;
        private string tempPath;
        private string gamesPath;
        private string gameDir;

        // Files
        private string gameZip;
        private string gameVer;

        // Bools
        public static bool gameInstalled;
        public static bool updateAvailable;
        public static bool downloadActive = false;

        private MC5Status _status;

        private readonly System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();

        internal MC5Status Status
        {
            get => _status;
            set
            {
                _status = value;
                switch (_status)
                {
                    case MC5Status.ready:
                        PlayBTN.Content = "Play";
                        PlayBTN.IsEnabled = true;
                        downloadActive = false;
                        gameInstalled = true;
                        break;
                    case MC5Status.noInstall:
                        PlayBTN.Content = "Download";
                        PlayBTN.IsEnabled = true;
                        downloadActive = false;
                        gameInstalled = false;
                        break;
                    case MC5Status.downloading:
                        PlayBTN.Content = "Downloading";
                        PlayBTN.IsEnabled = false;
                        downloadActive = true;
                        gameInstalled = false;
                        break;
                    case MC5Status.checkUpdate:
                        PlayBTN.Content = "Checking...";
                        PlayBTN.IsEnabled = false;
                        downloadActive = false;
                        gameInstalled = true;
                        break;
                    case MC5Status.update:
                        PlayBTN.Content = "Update";
                        PlayBTN.IsEnabled = true;
                        downloadActive = false;
                        gameInstalled = true;
                        break;
                    case MC5Status.updating:
                        PlayBTN.Content = "Updating";
                        PlayBTN.IsEnabled = false;
                        downloadActive = true;
                        gameInstalled = false;
                        break;
                    case MC5Status.installing:
                        PlayBTN.Content = "Installing";
                        PlayBTN.IsEnabled = false;
                        downloadActive = true;
                        gameInstalled = false;
                        break;
                    case MC5Status.error:
                        PlayBTN.Content = "Error";
                        PlayBTN.IsEnabled = false;
                        downloadActive = false;
                        gameInstalled = false;
                        break;
                }
            }
        }
        public Minecraft5View()
        {
            InitializeComponent();
            rootPath = Directory.GetCurrentDirectory();
            tempPath = Path.Combine(Path.GetTempPath(), "MCParodyLauncher");
            gameZip = Path.Combine(tempPath, $"{GameNameS.ToLower()}.zip");
            CheckInstall();
            if (gameInstalled == true) { CheckForUpdates(false); }
        }

        private void CheckInstall()
        {
            try
            {
                RegistryKey keyGame = Registry.CurrentUser.OpenSubKey(@$"{MainWindow.regPath}\{GameNameS.ToLower()}", true);
                Object obGameInstalled = keyGame.GetValue("Installed", null);
                if (obGameInstalled != null)
                {
                    if (obGameInstalled as String != "0")
                    {
                        Object obGamePath = keyGame.GetValue("InstallPath");
                        if (obGamePath != null) { gameDir = (obGamePath as String); }
                        Status = MC5Status.ready;
                    }
                    else { Status = MC5Status.noInstall; }
                }
                else { Status = MC5Status.noInstall; }
                keyGame.Close();
                return;
            }
            catch (Exception ex) { ErrorMSG(ex); return; }
        }

        private void GetInstallPath()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@$"{MainWindow.regPath}\{GameNameS.ToLower()}"))
                {
                    if (key != null)
                    {
                        Object obPath = key.GetValue("InstallPath");
                        if (obPath != null)
                        {
                            gameDir = (obPath as String);
                            key.Close();
                        }
                    }
                }
            }
            catch (Exception ex) { ErrorMSG(ex); return; }
        }

        private async Task CheckForUpdates(bool manual)
        {
            Status = MC5Status.checkUpdate;
            try
            {
                await Task.Run(() => GetInstallPath());
                gameVer = Path.Combine(gameDir, "version.txt");
                if (!File.Exists(gameVer)) { updateAvailable = true; }
                else
                {
                    Version localVer = new Version(File.ReadAllText(gameVer));
                    WebClient webClient = new();
                    Version onlineVer = new Version(await webClient.DownloadStringTaskAsync(verLink));
                    if (onlineVer.IsDifferentThan(localVer)) { updateAvailable = true; }
                    else { updateAvailable = false; }
                }
                if (manual && !updateAvailable) { MessageBox.Show("No updates are available.", $"{GameName}", MessageBoxButton.OK, MessageBoxImage.Information); }
                if (updateAvailable) { Status = MC5Status.update; }
                else { Status = MC5Status.ready; }
                return;
            }
            catch (Exception ex) { ErrorMSG(ex); return; }
        }

        private void DownloadGame()
        {
            try
            {
                ProgressBar.Visibility = Visibility.Visible;
                ProgressBar.IsIndeterminate = false;
                CreateTemp();
                if (Directory.Exists(gameDir)) { Directory.Delete(gameDir, true); }
                Directory.CreateDirectory(gameDir);
                if (File.Exists(gameZip)) { File.Delete(gameZip); }
                WebClient webClient = new WebClient();
                if (MainWindow.usDownloadStats == true) { DownloadStats.Visibility = Visibility.Visible; }
                stopwatch.Start();
                webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadGameComplete);
                webClient.DownloadProgressChanged += new DownloadProgressChangedEventHandler(ProgressChanged);
                webClient.DownloadFileAsync(new Uri(gameLink), gameZip);
                return;
            }
            catch (Exception ex) { ErrorMSG(ex); return; }
        }

        private void DownloadGameComplete(object sender, AsyncCompletedEventArgs e)
        {
            stopwatch.Reset();
            DownloadStats.Visibility = Visibility.Hidden;
            ExtractZipAsync(gameZip, gameDir);
            RegistryKey key = Registry.CurrentUser.OpenSubKey(@$"{MainWindow.regPath}\{GameNameS.ToLower()}", true);
            key.SetValue("Installed", "1");
            key.Close();
            return;
        }

        private async void PlayBTN_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Status == MC5Status.update) { Status = MC5Status.updating; DownloadGame(); }
                if (Status == MC5Status.noInstall)
                {
                    InstallGame.installConfirmed = false;
                    InstallGame.installCanceled = false;
                    WebClient webClient = new WebClient();
                    string gameSize = webClient.DownloadString(sizeLink);
                    RegistryKey keyGames = Registry.CurrentUser.OpenSubKey($"{MainWindow.regPath}", true);
                    keyGames.CreateSubKey($"{GameNameS.ToLower()}");
                    RegistryKey keyGame = Registry.CurrentUser.OpenSubKey(@$"{MainWindow.regPath}\{GameNameS.ToLower()}", true);
                    keyGames.Close();
                    InstallGame installWindow = new InstallGame($"{GameName}", gameSize);
                    installWindow.Show();
                    PlayBTN.IsEnabled = false;
                    downloadActive = true;
                    while (InstallGame.installConfirmed == false)
                    {
                        if (InstallGame.installCanceled == true) { PlayBTN.IsEnabled = true; downloadActive = false; return; }
                        await Task.Delay(100);
                    }
                    downloadActive = false;
                    gameDir = Path.Combine(InstallGame.InstallPath, $"{GameName}");
                    keyGame.SetValue("InstallPath", gameDir);
                    keyGame.Close();
                    Status = MC5Status.downloading;
                    DownloadGame();
                    return;
                }
                if (Status == MC5Status.downloading || Status == MC5Status.updating) { return; }
                if (!Keyboard.IsKeyDown(Key.LeftShift))
                {
                    if (Process.GetProcessesByName($"{GameProcess}").Length > 0)
                    {
                        MessageBox.Show($"{GameName} is already running.", $"{GameName}", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                GetInstallPath();
                Process.Start(Path.Combine(gameDir, $"{GameProcess}.exe"));
                LaunchingGame launchWindow = new LaunchingGame($"{GameName}");
                launchWindow.Show();
                await Task.Delay(1500);
                if (MainWindow.usHideWindow == true)
                {
                    Application.Current.MainWindow.Hide();
                    bool gameRunning = true;
                    while (gameRunning == true)
                    {
                        await Task.Delay(100);
                        if (Process.GetProcessesByName(LaunchingGame.mc5Proc).Length > 0) { gameRunning = true; }
                        else { gameRunning = false; }
                    }
                    await Task.Delay(150);
                    Application.Current.MainWindow.Show();
                    Application.Current.MainWindow.Activate();
                }
            }
            catch (Exception ex) { ErrorMSG(ex); return; }
        }

        private async Task ExtractZipAsync(string zipfile, string output)
        {
            try
            {
                ProgressBar.Visibility = Visibility.Visible;
                ProgressBar.IsIndeterminate = true;
                Status = MC5Status.installing;
                await Task.Run(() => ZipFile.ExtractToDirectory(zipfile, output));
                File.Delete(gameZip);
                ProgressBar.Visibility = Visibility.Hidden;
                if (MainWindow.usNotifications == true)
                {
                    string dlStatus;
                    if (Status == MC5Status.updating) { dlStatus = "updating"; }
                    else { dlStatus = "downloading"; }
                    new ToastContentBuilder()
                    .AddText("Download complete!")
                    .AddText($"{GameName} has finished {dlStatus}.")
                    .Show();
                }
                Status = MC5Status.ready;
                return;
            }
            catch (Exception ex) { ErrorMSG(ex); return; }
        }

        private void ProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            try
            {
                string downloadProgress = e.ProgressPercentage + "%";
                string downloadSpeed = string.Format("{0} MB/s", (e.BytesReceived / 1024.0 / 1024.0 / stopwatch.Elapsed.TotalSeconds).ToString("0.00"));
                string downloadedMBs = Math.Round(e.BytesReceived / 1024.0 / 1024.0) + " MB";
                string totalMBs = Math.Round(e.TotalBytesToReceive / 1024.0 / 1024.0) + " MB";
                string progress = $"{downloadedMBs} / {totalMBs} ({downloadProgress}) ({downloadSpeed})";
                DownloadStats.Content = progress;
                ProgressBar.Value = e.ProgressPercentage;
            }
            catch (Exception ex) { ErrorMSG(ex); return; }
        }

        private void CreateTemp() { Directory.CreateDirectory(tempPath); return; }

        private void ErrorMSG(Exception exception) { Status = MC5Status.error; Dispatcher.BeginInvoke(new Action(() => System.Windows.MessageBox.Show($"{exception}", "Error", MessageBoxButton.OK, MessageBoxImage.Error)), System.Windows.Threading.DispatcherPriority.Normal); return; }

        private void PlayContext_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            CheckInstall();
            if (!gameInstalled)
            {
                CheckForUpdatesBTN.IsEnabled = false;
                DesktopShortcut.IsEnabled = false;
                FileLocation.IsEnabled = false;
                LocateInstall.IsEnabled = true;
                MoveInstall.IsEnabled = false;
                Uninstall.IsEnabled = false;
            }
            else
            {
                CheckForUpdatesBTN.IsEnabled = true;
                DesktopShortcut.IsEnabled = true;
                FileLocation.IsEnabled = true;
                LocateInstall.IsEnabled = false;
                MoveInstall.IsEnabled = true;
                Uninstall.IsEnabled = true;
            }
        }

        private void StorePage_Click(object sender, RoutedEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo(gameStore) { UseShellExecute = true }); }
            catch (Exception ex) { ErrorMSG(ex); return; }
        }

        private void CheckForUpdatesBTN_Click(object sender, RoutedEventArgs e)
        {
            try { CheckForUpdates(true); }
            catch (Exception ex) { ErrorMSG(ex); return; }
        }

        private void DesktopShortcut_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                GetInstallPath();
                object shDesktop = (object)"Desktop";
                Wsh.WshShell shell = new Wsh.WshShell();
                string shortcutAddress = (string)shell.SpecialFolders.Item(ref shDesktop) + @$"\{GameName}.lnk";
                Wsh.IWshShortcut shortcut = (Wsh.IWshShortcut)shell.CreateShortcut(shortcutAddress);
                shortcut.TargetPath = gameDir + $"\\{GameProcess}.exe";
                shortcut.IconLocation = gameDir + "\\www\\icon\\icon.ico";
                shortcut.Save();
            }
            catch (Exception ex) { ErrorMSG(ex); return; }
        }

        private void FileLocation_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                GetInstallPath();
                Process.Start(new ProcessStartInfo { FileName = gameDir, UseShellExecute = true });
            }
            catch (Exception ex) { ErrorMSG(ex); return; }
        }

        private void LocateInstall_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MessageBox.Show($"Please select the folder that containes \"{GameProcess}.exe\".", $"{GameName}", MessageBoxButton.OK, MessageBoxImage.Information);
                WinForms.FolderBrowserDialog selectInstallDialog = new WinForms.FolderBrowserDialog();
                selectInstallDialog.SelectedPath = System.AppDomain.CurrentDomain.BaseDirectory;
                selectInstallDialog.ShowNewFolderButton = true;
                WinForms.DialogResult gameResult = selectInstallDialog.ShowDialog();
                if (gameResult == WinForms.DialogResult.OK)
                {
                    if (!File.Exists(Path.Combine(selectInstallDialog.SelectedPath, $"{GameProcess}.exe"))) { MessageBox.Show("Please select the location with Minecraft2Remake.exe", "Minecraft Parody Launcher", MessageBoxButton.OK, MessageBoxImage.Exclamation); return; }
                    gameDir = Path.Combine(selectInstallDialog.SelectedPath);
                    RegistryKey keyGames = Registry.CurrentUser.OpenSubKey($"{MainWindow.regPath}", true);
                    keyGames.CreateSubKey($"{GameNameS.ToLower()}");
                    RegistryKey keyGame = Registry.CurrentUser.OpenSubKey(@$"{MainWindow.regPath}\{GameNameS.ToLower()}", true);
                    keyGames.SetValue("InstallPath", gameDir);
                    keyGames.SetValue("Installed", "1");
                    keyGame.Close();
                    keyGames.Close();
                    Status = MC5Status.ready;
                }
                return;
            }
            catch (Exception ex) { ErrorMSG(ex); return; }
        }

        private void MoveInstall_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                GetInstallPath();
                MessageBox.Show($"Please select where you would like to move {GameName} to.\n\nA folder called \"{GameName}\" will be created.", "Move Install", MessageBoxButton.OK, MessageBoxImage.Information);
                WinForms.FolderBrowserDialog moveInstallDialog = new WinForms.FolderBrowserDialog();
                moveInstallDialog.SelectedPath = System.AppDomain.CurrentDomain.BaseDirectory;
                moveInstallDialog.ShowNewFolderButton = true;
                WinForms.DialogResult moveResult = moveInstallDialog.ShowDialog();
                if (moveResult == WinForms.DialogResult.OK)
                {
                    string dirOld = gameDir;
                    gameDir = Path.Combine(moveInstallDialog.SelectedPath, $"{GameName}");
                    RegistryKey keyGame = Registry.CurrentUser.OpenSubKey(@$"{MainWindow.regPath}\{GameNameS.ToLower()}", true);
                    keyGame.SetValue("InstallPath", gameDir);
                    keyGame.Close();
                    MessageBox.Show("Minecraft Parody Launcher may freeze during the move.\n\nDo not panic.", "Move Install", MessageBoxButton.OK, MessageBoxImage.Information);
                    bool sameVolume = dirOld.Substring(0, 1).Equals(gameDir.Substring(0, 1));
                    if (sameVolume == true)
                    {
                        Directory.Move(dirOld, gameDir);
                        MessageBox.Show("Install has been moved!", $"{GameName}", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        Directory.CreateDirectory(gameDir);
                        foreach (string dirPath in Directory.GetDirectories(dirOld, "*", SearchOption.AllDirectories)) { Directory.CreateDirectory(dirPath.Replace(dirOld, gameDir)); }
                        foreach (string newPath in Directory.GetFiles(dirOld, "*.*", SearchOption.AllDirectories)) { File.Copy(newPath, newPath.Replace(dirOld, gameDir), true); }
                        Directory.Delete(dirOld, true);
                        MessageBox.Show("Install has been moved!", $"{GameName}", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                return;
            }
            catch (Exception ex) { ErrorMSG(ex); return; }
        }

        private void Uninstall_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                GetInstallPath();
                MessageBoxResult uninstallMSG = System.Windows.MessageBox.Show($"Are you sure you want to uninstall {GameName}?", $"{GameName}", System.Windows.MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (uninstallMSG == MessageBoxResult.Yes)
                {
                    Directory.Delete(gameDir, true);
                    RegistryKey key = Registry.CurrentUser.OpenSubKey(@$"{MainWindow.regPath}\{GameNameS.ToLower()}", true);
                    key.SetValue("Installed", "0");
                    key.Close();
                    Status = MC5Status.noInstall;
                    MessageBox.Show($"{GameName} has been successfully uninstalled!", $"{GameName}", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
                return;
            }
            catch (Exception ex) { ErrorMSG(ex); return; }
        }

        struct Version
        {
            internal static Version zero = new Version(0, 0, 0);

            private short major;
            private short minor;
            private short subMinor;

            internal Version(short _major, short _minor, short _subMinor)
            {
                major = _major;
                minor = _minor;
                subMinor = _subMinor;
            }
            internal Version(string _version)
            {
                string[] versionStrings = _version.Split('.');
                if (versionStrings.Length != 3)
                {
                    major = 0;
                    minor = 0;
                    subMinor = 0;
                    return;
                }

                major = short.Parse(versionStrings[0]);
                minor = short.Parse(versionStrings[1]);
                subMinor = short.Parse(versionStrings[2]);
            }

            internal bool IsDifferentThan(Version _otherVersion)
            {
                if (major != _otherVersion.major)
                {
                    return true;
                }
                else
                {
                    if (minor != _otherVersion.minor)
                    {
                        return true;
                    }
                    else
                    {
                        if (subMinor != _otherVersion.subMinor)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            public override string ToString()
            {
                return $"{major}.{minor}.{subMinor}";
            }
        }
    }
}

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using Newtonsoft.Json;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;

using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.Forms.MessageBox;
using TextBox = System.Windows.Controls.TextBox;
using System.Threading;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.ComponentModel;

namespace Balatro_Loader
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<Mod> Mods { get; set; }
        public ObservableCollection<StoredMod> StoredMods { get; set; }
        public ObservableCollection<Profile> Profiles { get; set; }
        private string gameDirectory = @"C:\Program Files (x86)\Steam\steamapps\common\Balatro";
        private string baloaderDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Baloader");
        private string dependenciesDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Baloader", "Dependencies");
        private string profilesDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Baloader", "Profiles");
        private string modsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Baloader", "Mods");
        private string appDataModsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Balatro", "Mods");
        private string currentProfile;
        private Profile defaultProfile;
        public string CurrentProfile
        {
            get => currentProfile;
            set
            {
                if (currentProfile != value)
                {
                    currentProfile = value;
                    OnPropertyChanged(nameof(CurrentProfile));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public MainWindow()
        {
            InitializeComponent();
            CreateDirectories();
            Mods = new ObservableCollection<Mod>();
            StoredMods = new ObservableCollection<StoredMod>();
            Profiles = new ObservableCollection<Profile>();
            profileModsListView.ItemsSource = Mods;
            storedModsListView.ItemsSource = StoredMods;
            LoadStoredMods();
            LoadProfiles();
            SetCurrentProfile(currentProfile);
        }

        private void CreateDirectories()
        {
            Directory.CreateDirectory(baloaderDirectory);
            Directory.CreateDirectory(dependenciesDirectory);
            Directory.CreateDirectory(profilesDirectory);
            Directory.CreateDirectory(modsDirectory);
        }
        private void DeleteEmptyDirectories(string startLocation)
        {
            foreach (var directory in Directory.GetDirectories(startLocation))
            {
                DeleteEmptyDirectories(directory);
                if (Directory.GetFiles(directory).Length == 0 && Directory.GetDirectories(directory).Length == 0)
                {
                    Directory.Delete(directory, false);
                }
            }
        }

        private async void DownloadModButton_Click(object sender, RoutedEventArgs e)
        {
            string modUrl = modUrlTextBox.Text;
            if (string.IsNullOrEmpty(modUrl))
            {
                MessageBox.Show("Please enter a mod URL.");
                return;
            }

            try
            {
                await DownloadAndInstallMod(modUrl);
                MessageBox.Show("Mod downloaded successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error downloading mod: {ex.Message}");
            }
        }

        private async Task DownloadAndInstallMod(string url)
        {
            string downloadPath = await DownloadFileAsync(url, modsDirectory);
            string extractPath = Path.Combine(modsDirectory, Path.GetFileNameWithoutExtension(downloadPath));

            if (Path.GetExtension(downloadPath).Equals(".gz", StringComparison.OrdinalIgnoreCase))
            {
                await ExtractTarGz(downloadPath, extractPath);
            }
            else if (Path.GetExtension(downloadPath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                ExtractZip(downloadPath, extractPath);
            }
            else
            {
                throw new InvalidDataException("The downloaded file is not a valid .tar.gz or .zip archive.");
            }

            // Look for the first .lua file in the extracted folder
            string luaFilePath = Directory.GetFiles(extractPath, "*.lua", SearchOption.AllDirectories).FirstOrDefault();
            if (luaFilePath != null)
            {
                string luaFileParentDir = Path.GetDirectoryName(luaFilePath);
                string modName = Path.GetFileName(luaFileParentDir);

                // Move the folder containing the .lua file to the Mods directory
                string modDestinationPath = Path.Combine(modsDirectory, modName);
                if (!string.Equals(luaFileParentDir, modDestinationPath, StringComparison.OrdinalIgnoreCase))
                {
                    MoveDirectoryWithRetries(luaFileParentDir, modDestinationPath);
                }

                // Create a new Mod object
                Mod mod = new Mod
                {
                    Name = modName,
                    Version = "1.0",
                    Description = "Default description",
                    Author = new string[] { "Unknown" },
                    Dependencies = new List<Dependency> { },
                    Conflicts = new List<Conflict> { },
                    Status = "Downloaded",
                    DownloadUrl = url // Store the download URL
                };
                Mods.Add(mod);
                StoreMod(mod);

                // Copy the mod to the game directory
                string gameModDestinationPath = Path.Combine(appDataModsDirectory, modName);
                if (Directory.Exists(gameModDestinationPath))
                {
                    Directory.Delete(gameModDestinationPath, true);
                }
                DirectoryCopy(modDestinationPath, gameModDestinationPath, true);
            }
            else
            {
                MessageBox.Show("No .lua file found in the downloaded mod.");
            }

            // Delete empty directories
            DeleteEmptyDirectories(modsDirectory);

            LoadStoredMods();
        }

        private void MoveDirectoryWithRetries(string sourceDirPath, string destinationDirPath, int maxRetries = 3, int delayMilliseconds = 1000)
        {
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    if (Directory.Exists(destinationDirPath))
                    {
                        Directory.Delete(destinationDirPath, true);
                    }
                    Directory.Move(sourceDirPath, destinationDirPath);
                    return;
                }
                catch (IOException)
                {
                    if (attempt == maxRetries - 1)
                    {
                        throw;
                    }
                    Thread.Sleep(delayMilliseconds);
                }
            }
        }


        private string GetExtractedName(string extractPath)
        {
            var directories = Directory.GetDirectories(extractPath);
            if (directories.Length > 0)
            {
                return Path.GetFileName(directories[0]);
            }

            var files = Directory.GetFiles(extractPath);
            if (files.Length > 0)
            {
                return Path.GetFileNameWithoutExtension(files[0]);
            }

            return "Unknown Mod";
        }

        private async Task ExtractTarGz(string filePath, string outputDir)
        {
            try
            {
                // Decompress the .gz file to a .tar file
                using (Stream inStream = File.OpenRead(filePath))
                using (Stream gzipStream = new GZipInputStream(inStream))
                using (TarInputStream tarStream = new TarInputStream(gzipStream, System.Text.Encoding.UTF8))
                {
                    TarEntry entry;
                    while ((entry = tarStream.GetNextEntry()) != null)
                    {
                        if (entry.IsDirectory)
                        {
                            continue;
                        }

                        string entryPath = Path.Combine(outputDir, entry.Name);
                        string entryDir = Path.GetDirectoryName(entryPath);
                        if (!Directory.Exists(entryDir))
                        {
                            Directory.CreateDirectory(entryDir);
                        }

                        using (FileStream outStream = File.Create(entryPath))
                        {
                            await tarStream.CopyEntryContentsAsync(outStream, CancellationToken.None);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error extracting TAR.GZ file: {ex.Message}");
            }
        }

        private void ExtractZip(string filePath, string outputDir)
        {
            try
            {
                ZipFile.ExtractToDirectory(filePath, outputDir, System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error extracting ZIP file: {ex.Message}");
            }
        }



        private async Task<string> DownloadFileAsync(string url, string outputDir)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    byte[] fileBytes = await client.GetByteArrayAsync(url);
                    string fileName = Path.GetFileName(new Uri(url).AbsolutePath);
                    string outputPath = Path.Combine(outputDir, fileName);

                    Directory.CreateDirectory(outputDir); // Ensure the directory exists

                    using (FileStream fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
                    {
                        await fs.WriteAsync(fileBytes, 0, fileBytes.Length);
                    }
                    return outputPath;
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show($"Access to the path is denied: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error downloading file: {ex.Message}");
                throw;
            }
        }




        private async void ImportModButton_Click(object sender, RoutedEventArgs e)
        {
            using (var openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Mod files (*.zip;*.tar.gz)|*.zip;*.tar.gz|All files (*.*)|*.*";
                openFileDialog.Title = "Select a Mod File";

                if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string sourceFilePath = openFileDialog.FileName;
                    string fileName = Path.GetFileName(sourceFilePath);
                    string destinationFilePath = Path.Combine(modsDirectory, fileName);

                    try
                    {
                        // Copy the file to the mods directory
                        File.Copy(sourceFilePath, destinationFilePath, true);

                        // Extract the file if it is a .zip or .tar.gz
                        string extractPath = Path.Combine(modsDirectory, Path.GetFileNameWithoutExtension(destinationFilePath));
                        if (Path.GetExtension(destinationFilePath).Equals(".gz", StringComparison.OrdinalIgnoreCase))
                        {
                            await ExtractTarGz(destinationFilePath, extractPath);
                        }
                        else if (Path.GetExtension(destinationFilePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            ExtractZip(destinationFilePath, extractPath);
                        }

                        // Look for the first .lua file in the extracted folder
                        string luaFilePath = Directory.GetFiles(extractPath, "*.lua", SearchOption.AllDirectories).FirstOrDefault();
                        if (luaFilePath != null)
                        {
                            string luaFileParentDir = Path.GetDirectoryName(luaFilePath);
                            string modName = Path.GetFileName(luaFileParentDir);

                            // Move the folder containing the .lua file to the Mods directory
                            string modDestinationPath = Path.Combine(modsDirectory, modName);
                            if (!string.Equals(luaFileParentDir, modDestinationPath, StringComparison.OrdinalIgnoreCase))
                            {
                                MoveDirectoryWithRetries(luaFileParentDir, modDestinationPath);
                            }

                            // Create a new Mod object
                            Mod mod = new Mod
                            {
                                Name = modName,
                                Version = "1.0",
                                Description = "Default description",
                                Author = new string[] { "Unknown" },
                                Dependencies = new List<Dependency> { },
                                Conflicts = new List<Conflict> { },
                                Status = "Imported"
                            };
                            Mods.Add(mod);
                            StoreMod(mod);
                        }
                        else
                        {
                            MessageBox.Show("No .lua file found in the imported mod.");
                        }
                        DeleteEmptyDirectories(modsDirectory);
                        LoadStoredMods();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error importing mod: {ex.Message}");
                    }
                }
            }
        }


        private void ConvertOldMetadataToManifest(string modDirectory)
        {
            string[] luaFiles = Directory.GetFiles(modDirectory, "*.lua", SearchOption.TopDirectoryOnly);
            if (luaFiles.Length == 0)
            {
                MessageBox.Show("No .lua file found in the mod directory.");
                return;
            }

            // Find the lua file that is most like the mod name
            string luaFilePath = luaFiles.OrderBy(f => LevenshteinDistance(Path.GetFileNameWithoutExtension(f), new DirectoryInfo(modDirectory).Name)).FirstOrDefault();
            if (luaFilePath == null)
            {
                MessageBox.Show("No suitable .lua file found in the mod directory.");
                return;
            }

            string[] lines = File.ReadAllLines(luaFilePath);
            Mod mod = new Mod
            {
                Dependencies = new List<Dependency>(),
                Conflicts = new List<Conflict>(),
                Provides = new List<string>()
            };

            foreach (string line in lines)
            {
                if (line.StartsWith("--- MOD_NAME:"))
                {
                    mod.Name = line.Replace("--- MOD_NAME:", "").Trim();
                }
                else if (line.StartsWith("--- MOD_ID:"))
                {
                    mod.Id = line.Replace("--- MOD_ID:", "").Trim(new char[] { '[', ']' });
                }
                else if (line.StartsWith("--- BADGE_COLOR:"))
                {
                    mod.BadgeColour = line.Replace("--- BADGE_COLOR:", "").Trim();
                }
                else if (line.StartsWith("--- MOD_AUTHOR:"))
                {
                    mod.Author = line.Replace("--- MOD_AUTHOR:", "").Trim().Trim(new char[] { '[', ']' }).Split(',').Select(a => a.Trim()).ToArray();
                }
                else if (line.StartsWith("--- MOD_DESCRIPTION:"))
                {
                    mod.Description = line.Replace("--- MOD_DESCRIPTION:", "").Trim();
                }
                else if (line.StartsWith("--- PREFIX:"))
                {
                    mod.Prefix = line.Replace("--- PREFIX:", "").Trim();
                }
                else if (line.StartsWith("--- VERSION:"))
                {
                    mod.Version = line.Replace("--- VERSION:", "").Trim();
                }
                else if (line.StartsWith("--- PRIORITY:"))
                {
                    mod.Priority = int.Parse(line.Replace("--- PRIORITY:", "").Trim());
                }
                else if (line.StartsWith("--- DEPENDS:"))
                {
                    string depends = line.Replace("--- DEPENDS:", "").Trim();
                    foreach (string dependency in depends.Trim(new char[] { '[', ']' }).Split(','))
                    {
                        string[] parts = dependency.Split(new char[] { '>', '<', '=' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length == 2)
                        {
                            mod.Dependencies.Add(new Dependency
                            {
                                Id = parts[0].Trim(),
                                MinVersion = dependency.Contains(">=") ? parts[1].Trim() : null,
                                MaxVersion = dependency.Contains("<=") ? parts[1].Trim() : null
                            });
                        }
                    }
                }
                else if (line.StartsWith("--- CONFLICTS:"))
                {
                    string conflicts = line.Replace("--- CONFLICTS:", "").Trim();
                    foreach (string conflict in conflicts.Trim(new char[] { '[', ']' }).Split(','))
                    {
                        string[] parts = conflict.Split(new char[] { '>', '<', '=' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length == 2)
                        {
                            mod.Conflicts.Add(new Conflict
                            {
                                Id = parts[0].Trim(),
                                MinVersion = conflict.Contains(">=") ? parts[1].Trim() : null,
                                MaxVersion = conflict.Contains("<=") ? parts[1].Trim() : null
                            });
                        }
                    }
                }
            }

            // Check if the mod name is different from the current folder name
            string currentFolderName = new DirectoryInfo(modDirectory).Name;
            if (!string.Equals(currentFolderName, mod.Name, StringComparison.OrdinalIgnoreCase))
            {
                string newModDirectory = Path.Combine(Path.GetDirectoryName(modDirectory), mod.Name);
                Directory.Move(modDirectory, newModDirectory);
                modDirectory = newModDirectory;
            }

            // Retain the existing DownloadUrl if present
            string manifestPath = Path.Combine(modDirectory, "manifest.json");
            if (File.Exists(manifestPath))
            {
                Mod existingMod = JsonConvert.DeserializeObject<Mod>(File.ReadAllText(manifestPath));
                mod.DownloadUrl = existingMod.DownloadUrl;
            }

            StoreMod(mod, modDirectory);
            MessageBox.Show("Old metadata converted to manifest.json successfully.");
        }
        private int LevenshteinDistance(string s, string t)
        {
            int[,] d = new int[s.Length + 1, t.Length + 1];

            for (int i = 0; i <= s.Length; i++)
                d[i, 0] = i;
            for (int j = 0; j <= t.Length; j++)
                d[0, j] = j;

            for (int i = 1; i <= s.Length; i++)
            {
                for (int j = 1; j <= t.Length; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }

            return d[s.Length, t.Length];
        }
        private void StoreMod(Mod mod, string modDirectory)
        {
            if (modDirectory == null)
            {
                throw new ArgumentNullException(nameof(modDirectory), "modDirectory cannot be null.");
            }

            if (mod.Name == null)
            {
                throw new ArgumentNullException(nameof(mod.Name), "mod.Name cannot be null.");
            }

            // Store mod properties in a JSON file
            string json = JsonConvert.SerializeObject(mod, Formatting.Indented);
            File.WriteAllText(Path.Combine(modDirectory, "manifest.json"), json);
        }





        private async void DownloadDependenciesButton_Click(object sender, RoutedEventArgs e)
        {
            await DownloadAndInstallDependencies();
        }

        private async Task DownloadAndInstallDependencies()
        {
            string lovelyUrl = "https://github.com/ethangreen-dev/lovely-injector/releases/download/v0.6.0/lovely-x86_64-pc-windows-msvc.zip"; // Replace with the actual URL
            string steamoddedUrl = "https://github.com/Steamopollys/Steamodded/archive/refs/heads/main.zip"; // Replace with the actual URL
            string lovelyDownloadPath = Path.Combine(dependenciesDirectory, "lovely.zip");
            string steamoddedDownloadPath = Path.Combine(dependenciesDirectory, "steamodded.zip");
            string versionDllPath = Path.Combine(dependenciesDirectory, "version.dll");
            string steamoddedFolderPath = Path.Combine(dependenciesDirectory, "Steamodded");

            try
            {
                if (File.Exists(lovelyDownloadPath) || File.Exists(steamoddedDownloadPath) || File.Exists(versionDllPath) || Directory.Exists(steamoddedFolderPath))
                {
                    var result = MessageBox.Show("Dependencies already exist. Would you like to update them?", "Update Dependencies", MessageBoxButtons.YesNo);
                    if (result == System.Windows.Forms.DialogResult.Yes)
                    {
                        Directory.Delete(dependenciesDirectory, true);
                        Directory.CreateDirectory(dependenciesDirectory);
                    }
                    else
                    {
                        return;
                    }
                }

                downloadStatusTextBlock.Text = "Download Status: Downloading Lovely...";
                string lovelyPath = await DownloadFileAsync(lovelyUrl, lovelyDownloadPath);
                if (lovelyPath == null)
                {
                    MessageBox.Show("Failed to download Lovely.");
                    return;
                }

                downloadStatusTextBlock.Text = "Download Status: Downloading Steamodded...";
                string steamoddedPath = await DownloadFileAsync(steamoddedUrl, steamoddedDownloadPath);
                if (steamoddedPath == null)
                {
                    MessageBox.Show("Failed to download Steamodded.");
                    return;
                }

                downloadStatusTextBlock.Text = "Download Status: Installing Lovely and Steamodded...";

                ExtractZip(lovelyDownloadPath, dependenciesDirectory);
                ExtractZip(steamoddedDownloadPath, dependenciesDirectory);
                InstallLovely(lovelyDownloadPath);
                InstallSteamodded(steamoddedDownloadPath);
                downloadStatusTextBlock.Text = "Download Status: Completed";
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show($"Access to the path is denied: {ex.Message}");
            }
            catch (Exception ex)
            {
                downloadStatusTextBlock.Text = "Download Status: Error";
                MessageBox.Show($"Error downloading Lovely or Steamodded: {ex.Message}");
            }
        }





        private bool IsFileLocked(string filePath)
        {
            try
            {
                using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    stream.Close();
                }
            }
            catch (IOException)
            {
                return true;
            }
            return false;
        }

        private void InstallLovely(string filePath)
        {
            string extractPath = dependenciesDirectory;
            string sourceFile = Path.Combine(extractPath, "version.dll");
            string destinationFile = Path.Combine(gameDirectory, "version.dll");

            if (File.Exists(sourceFile))
            {
                File.Copy(sourceFile, destinationFile, true);
            }
            else
            {
                MessageBox.Show("version.dll not found in the extracted files.");
            }
        }

        private void InstallSteamodded(string filePath)
        {
            string extractPath = dependenciesDirectory;
            string destinationPath = Path.Combine(appDataModsDirectory, "Steamodded");

            if (!Directory.Exists(appDataModsDirectory))
            {
                Directory.CreateDirectory(appDataModsDirectory);
            }

            if (Directory.Exists(Path.Combine(extractPath, "Steamodded-main")))
            {
                if (Directory.Exists(destinationPath))
                {
                    Directory.Delete(destinationPath, true);
                }
                Directory.Move(Path.Combine(extractPath, "Steamodded-main"), destinationPath);
            }
            else
            {
                MessageBox.Show("Steamodded not found in the extracted files.");
            }
        }

        private void SetGameDirectoryButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select the game directory";
                dialog.SelectedPath = gameDirectory;

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    gameDirectory = dialog.SelectedPath;
                    MessageBox.Show($"Game directory set to: {gameDirectory}");
                }
            }
        }

        private void CreateProfileButton_Click(object sender, RoutedEventArgs e)
        {
            var inputDialog = new InputDialog("Enter profile name:", "Create New Profile");
            if (inputDialog.ShowDialog() == true)
            {
                string profileName = inputDialog.ResponseText;
                if (string.IsNullOrEmpty(profileName))
                {
                    MessageBox.Show("Profile name cannot be empty.");
                    return;
                }

                string profilePath = Path.Combine(profilesDirectory, profileName);
                if (Directory.Exists(profilePath))
                {
                    MessageBox.Show("Profile already exists.");
                    return;
                }

                Directory.CreateDirectory(profilePath);
                var newProfile = new Profile { Name = profileName };
                Profiles.Add(newProfile);
                SetCurrentProfile(profileName);
            }
        }

        private void OpenProfileSelectionWindowButton_Click(object sender, RoutedEventArgs e)
        {
            LoadProfiles();
            var profileSelectionWindow = new ProfileSelectionWindow(new ObservableCollection<Profile>(Profiles));
            if (profileSelectionWindow.ShowDialog() == true && profileSelectionWindow.SelectedProfile != null)
            {
                SetCurrentProfile(profileSelectionWindow.SelectedProfile.Name);
            }
            else
            {
                MessageBox.Show("No profile selected.");
            }
        }

        private void SetCurrentProfile(string profileName)
        {
            // Find the profile by name
            Profile profile = Profiles.FirstOrDefault(p => p.Name == profileName);
            if (profile != null)
            {
                // Set the current profile
                CurrentProfile = profileName;
                defaultProfile = profile;

                // Load the mods for the selected profile
                LoadProfileMods(profileName);

                MessageBox.Show($"Profile changed to: {profileName}");
            }
            else
            {
                MessageBox.Show("Profile not found.");
            }
        }





        private void LoadProfileMods(string profileName)
        {
            string profileFilePath = Path.Combine(profilesDirectory, $"{profileName}.json");
            if (File.Exists(profileFilePath))
            {
                string json = File.ReadAllText(profileFilePath);
                Profile profile = JsonConvert.DeserializeObject<Profile>(json);
                if (profile != null)
                {
                    var existingProfile = Profiles.FirstOrDefault(p => p.Name == profileName);
                    if (existingProfile != null)
                    {
                        existingProfile.Mods.Clear();
                        foreach (var mod in profile.Mods)
                        {
                            mod.IsInstalled = Directory.Exists(Path.Combine(appDataModsDirectory, mod.Name));
                            mod.Status = mod.IsInstalled ? "Installed" : "Not Installed";
                            existingProfile.Mods.Add(mod);
                        }
                        profileModsListView.ItemsSource = existingProfile.Mods;
                    }
                }
            }
        }

        private void SaveProfile(Profile profile)
        {
            if (!Directory.Exists(profilesDirectory))
            {
                Directory.CreateDirectory(profilesDirectory);
            }

            string filePath = Path.Combine(profilesDirectory, $"{profile.Name}.json");
            string json = JsonConvert.SerializeObject(profile, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }
        private async void InstallModsButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(currentProfile))
            {
                MessageBox.Show("No profile selected.");
                return;
            }

            var profile = Profiles.FirstOrDefault(p => p.Name == currentProfile);
            if (profile != null)
            {
                var missingMods = new List<Mod>();
                var profileModNames = profile.Mods.Select(m => m.Name).ToList();
                profileModNames.Add("Steamodded");
                var installedMods = Directory.GetDirectories(appDataModsDirectory).Select(Path.GetFileName).ToList();
                var unwantedMods = installedMods.Except(profileModNames).ToList();

                foreach (var unwantedMod in unwantedMods)
                {
                    var unwantedModPath = Path.Combine(appDataModsDirectory, unwantedMod);
                    Directory.Delete(unwantedModPath, true);
                }

                foreach (var mod in profile.Mods)
                {
                    string modSourcePath = Path.Combine(modsDirectory, mod.Name);
                    string modDestinationPath = Path.Combine(appDataModsDirectory, mod.Name);

                    if (Directory.Exists(modSourcePath))
                    {
                        if (Directory.Exists(modDestinationPath))
                        {
                            Directory.Delete(modDestinationPath, true);
                        }
                        DirectoryCopy(modSourcePath, modDestinationPath, true);
                        mod.IsInstalled = true;
                        mod.Status = "Installed";
                    }
                    else
                    {
                        missingMods.Add(mod);
                    }
                }

                if (missingMods.Any())
                {
                    var result = MessageBox.Show("You are missing some mods, would you like to download them?", "Missing Mods", MessageBoxButtons.YesNo);
                    if (result == System.Windows.Forms.DialogResult.Yes)
                    {
                        foreach (var missingMod in missingMods)
                        {
                            if (!string.IsNullOrEmpty(missingMod.DownloadUrl))
                            {
                                try
                                {
                                    await DownloadAndInstallMod(missingMod.DownloadUrl);
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show($"Error downloading mod {missingMod.Name}: {ex.Message}");
                                }
                            }
                            else
                            {
                                MessageBox.Show($"No download URL available for mod {missingMod.Name}.");
                            }
                        }
                    }
                }
                profileModsListView.Items.Refresh();
            }
            else
            {
                MessageBox.Show("Profile not found.");
            }
        }
        private static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs, bool overwrite = false)
        {
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);
            DirectoryInfo[] dirs = dir.GetDirectories();

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException("Source directory does not exist or could not be found: " + sourceDirName);
            }

            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }

            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string tempPath = Path.Combine(destDirName, file.Name);
                file.CopyTo(tempPath, overwrite);
            }

            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string tempPath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, tempPath, copySubDirs, overwrite);
                }
            }
        }
        private void RefreshModsButton_Click(object sender, RoutedEventArgs e)
        {
            LoadStoredMods();
        }

        private static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);
            DirectoryInfo[] dirs = dir.GetDirectories();

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException("Source directory does not exist or could not be found: " + sourceDirName);
            }

            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }

            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string tempPath = Path.Combine(destDirName, file.Name);
                file.CopyTo(tempPath, false);
            }

            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string tempPath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, tempPath, copySubDirs);
                }
            }
        }

        private void StoreMod(Mod mod)
        {
            string modPath = Path.Combine(modsDirectory, mod.Name);
            if (!Directory.Exists(modPath))
            {
                Directory.CreateDirectory(modPath);
            }
            string json = JsonConvert.SerializeObject(mod, Formatting.Indented);
            File.WriteAllText(Path.Combine(modPath, "manifest.json"), json);
        }


        private void ConvertOldMetadataButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var modDirectory in Directory.GetDirectories(modsDirectory))
            {
                ConvertOldMetadataToManifest(modDirectory);
            }
            LoadStoredMods();
        }




        private void LoadStoredMods()
        {
            StoredMods.Clear();
            Mods.Clear();
            if (!Directory.Exists(modsDirectory))
            {
                Directory.CreateDirectory(modsDirectory);
            }

            foreach (var dir in Directory.GetDirectories(modsDirectory))
            {
                string modName = Path.GetFileName(dir);
                StoredMods.Add(new StoredMod { Name = modName });

                string modInfoPath = Path.Combine(dir, "manifest.json");
                if (File.Exists(modInfoPath))
                {
                    string json = File.ReadAllText(modInfoPath);
                    Mod mod = JsonConvert.DeserializeObject<Mod>(json);
                    Mods.Add(mod);
                }
            }
        }


        private void AddModToProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is StoredMod storedMod)
            {
                var mod = Mods.FirstOrDefault(m => m.Name == storedMod.Name);
                var profile = Profiles.FirstOrDefault(p => p.Name == currentProfile);
                if (mod != null && profile != null && !profile.Mods.Contains(mod))
                {
                    profile.Mods.Add(mod);
                    profileModsListView.Items.Refresh();
                    SaveProfile(profile);
                }
            }
        }


        private void RemoveModFromProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is Mod mod)
            {
                var profile = Profiles.FirstOrDefault(p => p.Name == currentProfile);
                if (profile != null && profile.Mods.Contains(mod))
                {
                    profile.Mods.Remove(mod);
                    profileModsListView.Items.Refresh();
                    SaveProfile(profile);
                }
            }
        }




        private void EditModButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is StoredMod storedMod)
            {
                var mod = Mods.FirstOrDefault(m => m.Name == storedMod.Name);
                if (mod == null)
                {
                    string modPath = Path.Combine(modsDirectory, storedMod.Name, "manifest.json");
                    if (File.Exists(modPath))
                    {
                        string json = File.ReadAllText(modPath);
                        mod = JsonConvert.DeserializeObject<Mod>(json);
                        Mods.Add(mod);
                    }
                    else
                    {
                        mod = new Mod
                        {
                            Id = Guid.NewGuid().ToString(),
                            Name = storedMod.Name,
                            DisplayName = "Default Display Name",
                            Author = new string[] { "Unknown" },
                            Description = "Default description",
                            Prefix = "",
                            MainFile = "",
                            Priority = 0,
                            BadgeColour = "",
                            BadgeTextColour = "",
                            Version = "1.0",
                            Dependencies = new List<Dependency>(),
                            Conflicts = new List<Conflict>(),
                            Provides = new List<string>(),
                            DumpLoc = false,
                            Status = "Not Installed"
                        };
                        Mods.Add(mod);
                        StoreMod(mod);
                    }
                }

                if (mod != null)
                {
                    var editDialog = new ModEdit(mod);
                    if (editDialog.ShowDialog() == true)
                    {
                        string oldModPath = Path.Combine(modsDirectory, storedMod.Name);
                        string newModPath = Path.Combine(modsDirectory, mod.Name);
                        if (!string.Equals(oldModPath, newModPath, StringComparison.OrdinalIgnoreCase))
                        {
                            Directory.Move(oldModPath, newModPath);
                        }
                        storedMod.Name = mod.Name;
                        StoreMod(mod);
                        LoadStoredMods();
                    }
                }
            }
        }
        private ObservableCollection<Profile> LoadProfilesFromStorage()
        {
            var profiles = new ObservableCollection<Profile>();

            if (!Directory.Exists(profilesDirectory))
            {
                Directory.CreateDirectory(profilesDirectory);
            }

            foreach (var file in Directory.GetFiles(profilesDirectory, "*.json"))
            {
                string json = File.ReadAllText(file);
                Profile profile = JsonConvert.DeserializeObject<Profile>(json);
                profiles.Add(profile);
            }

            return profiles;
        }
        private void LoadProfiles()
        {
            Profiles = LoadProfilesFromStorage();

            // Check if any profiles are loaded
            if (Profiles.Count == 0)
            {
                // Create a default profile if none exist
                defaultProfile = new Profile
                {
                    Name = "Default",
                    Mods = new ObservableCollection<Mod>()
                };
                Profiles.Add(defaultProfile);
                SaveProfile(defaultProfile);
            }
            else
            {
                // Set the default profile to the profile named "Default"
                defaultProfile = Profiles.FirstOrDefault(p => p.Name == "Default") ?? Profiles.First();
            }

            // Set the current profile to the default profile
            SetCurrentProfile(defaultProfile.Name);
        }



        public class StoredMod
        {
            public string Name { get; set; }
        }

        public class InputDialog : Window
        {
            private TextBox inputTextBox;
            public string ResponseText { get; private set; }

            public InputDialog(string question, string title)
            {
                Title = title;
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
                Width = 300;
                Height = 150;

                StackPanel stackPanel = new StackPanel();
                stackPanel.Children.Add(new TextBlock { Text = question });
                inputTextBox = new TextBox { Width = 200 };
                stackPanel.Children.Add(inputTextBox);

                Button okButton = new Button { Content = "OK", Width = 75, Margin = new Thickness(5) };
                okButton.Click += OkButton_Click;
                stackPanel.Children.Add(okButton);

                Content = stackPanel;
            }

            private void OkButton_Click(object sender, RoutedEventArgs e)
            {
                ResponseText = inputTextBox.Text;
                DialogResult = true;
            }
        }

        public class DescriptionConverter : JsonConverter
        {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                if (value is string[] descriptions)
                {
                    writer.WriteStartArray();
                    foreach (var description in descriptions)
                    {
                        writer.WriteValue(description);
                    }
                    writer.WriteEndArray();
                }
                else
                {
                    writer.WriteValue(value.ToString());
                }
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.StartArray)
                {
                    var descriptions = new List<string>();
                    while (reader.Read() && reader.TokenType != JsonToken.EndArray)
                    {
                        descriptions.Add(reader.Value.ToString());
                    }
                    return string.Join(" ", descriptions);
                }
                return reader.Value != null ? reader.Value.ToString() : null;
            }

            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(string);
            }
        }

        public class AuthorConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(string[]);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                JToken token = JToken.Load(reader);
                if (token.Type == JTokenType.String)
                {
                    return new string[] { token.ToString() };
                }
                else if (token.Type == JTokenType.Array)
                {
                    return token.ToObject<string[]>();
                }
                else if (token.Type == JTokenType.Null)
                {
                    return null;
                }
                throw new JsonSerializationException("Unexpected token type: " + token.Type);
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                string[] authors = (string[])value;
                if (authors.Length == 1)
                {
                    writer.WriteValue(authors[0]);
                }
                else
                {
                    writer.WriteStartArray();
                    foreach (string author in authors)
                    {
                        writer.WriteValue(author);
                    }
                    writer.WriteEndArray();
                }
            }
        }
    }
}


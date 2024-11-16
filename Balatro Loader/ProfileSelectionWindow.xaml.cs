using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Newtonsoft.Json;
using static Balatro_Loader.MainWindow;

namespace Balatro_Loader
{
    public partial class ProfileSelectionWindow : Window
    {
        public ObservableCollection<Profile> Profiles { get; set; }
        public Profile SelectedProfile { get; private set; }

        public ProfileSelectionWindow(ObservableCollection<Profile> profiles)
        {
            InitializeComponent();
            Profiles = profiles;
            profilesListView.ItemsSource = Profiles;
        }

        private void ProfilesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedProfile = (Profile)((ListView)sender).SelectedItem;
        }

        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            while (true)
            {
                var inputDialog = new InputDialog("Enter profile name:", "Create New Profile");
                if (inputDialog.ShowDialog() == true)
                {
                    string profileName = inputDialog.ResponseText;
                    if (string.IsNullOrEmpty(profileName))
                    {
                        MessageBox.Show("Profile name cannot be empty.");
                        continue;
                    }

                    string profileFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Baloader", "Profiles", $"{profileName}.json");
                    if (File.Exists(profileFilePath))
                    {
                        MessageBox.Show("Profile already exists.");
                        continue;
                    }

                    var newProfile = new Profile { Name = profileName };
                    Profiles.Add(newProfile);
                    SelectedProfile = newProfile;

                    // Save the new profile as a .json file
                    string json = JsonConvert.SerializeObject(newProfile, Formatting.Indented);
                    File.WriteAllText(profileFilePath, json);
                    DialogResult = true;
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedProfile == null)
            {
                MessageBox.Show("No profile selected.");
                return;
            }

            var result = MessageBox.Show($"Are you sure you want to delete the profile '{SelectedProfile.Name}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                string profileFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Baloader", "Profiles", $"{SelectedProfile.Name}.json");
                if (File.Exists(profileFilePath))
                {
                    File.Delete(profileFilePath);
                }

                Profiles.Remove(SelectedProfile);
                SelectedProfile = null;
            }
        }

        private void ExportProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedProfile != null)
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "JSON files (*.json)|*.json",
                    DefaultExt = "json",
                    AddExtension = true
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    string json = SelectedProfile.ExportToJson();
                    File.WriteAllText(saveFileDialog.FileName, json);
                    MessageBox.Show("Profile exported successfully.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                MessageBox.Show("No profile selected.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportProfileButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json",
                DefaultExt = "json",
                AddExtension = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string json = File.ReadAllText(openFileDialog.FileName);
                Profile importedProfile = Profile.ImportFromJson(json);
                Profiles.Add(importedProfile);
                MessageBox.Show("Profile imported successfully.", "Import", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}

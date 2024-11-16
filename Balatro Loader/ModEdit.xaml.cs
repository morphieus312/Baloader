using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Newtonsoft.Json;
using static Balatro_Loader.MainWindow;

namespace Balatro_Loader
{
    public partial class ModEdit : Window
    {
        public Mod Mod { get; private set; }

        public ModEdit(Mod mod)
        {
            InitializeComponent();
            Mod = mod;
            nameTextBox.Text = mod.Name;
            versionTextBox.Text = mod.Version;
            descriptionTextBox.Text = mod.Description;
            authorTextBox.Text = string.Join(", ", mod.Author);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            Mod.Name = nameTextBox.Text;
            Mod.Version = versionTextBox.Text;
            Mod.Description = descriptionTextBox.Text;
            Mod.Author = authorTextBox.Text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                           .Select(author => author.Trim())
                                           .ToArray();
            DialogResult = true;
        }
    }

    public class Mod
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        [JsonConverter(typeof(AuthorConverter))]
        public string[] Author { get; set; }
        [JsonConverter(typeof(DescriptionConverter))]
        public string Description { get; set; }
        public string Prefix { get; set; }
        public string MainFile { get; set; }
        public int Priority { get; set; }
        public string BadgeColour { get; set; }
        public string BadgeTextColour { get; set; }
        public string Version { get; set; }
        public List<Dependency> Dependencies { get; set; }
        public List<Conflict> Conflicts { get; set; }
        public List<string> Provides { get; set; }
        public bool DumpLoc { get; set; }
        public string Status { get; set; }
        public bool IsInstalled { get; set; }
        public string DownloadUrl { get; set; }
    }
}

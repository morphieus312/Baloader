using Newtonsoft.Json;
using static Balatro_Loader.MainWindow;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System;
using System.Globalization;
using System.Windows.Data;

namespace Balatro_Loader
{
    public class ModMetadata
    {
        public string Id { get; set; }
        public string Name { get; set; }
        [JsonConverter(typeof(DescriptionConverter))]
        public List<string> Authors { get; set; }
        [JsonConverter(typeof(DescriptionConverter))]
        public string[] Description { get; set; }
        public string Prefix { get; set; }
        public string MainFile { get; set; }
        public int Priority { get; set; }
        public string BadgeColour { get; set; }
        public string BadgeTextColour { get; set; }
        public string DisplayName { get; set; }
        public string Version { get; set; }
        public List<Dependency> Dependencies { get; set; }
        public List<Conflict> Conflicts { get; set; }
        public List<string> Provides { get; set; }
        public bool DumpLoc { get; set; }
        public string DownloadLink { get; set; }
    }

    public class Dependency
    {
        public string Id { get; set; }
        public string MinVersion { get; set; }
        public string MaxVersion { get; set; }
    }

    public class Conflict
    {
        public string Id { get; set; }
        public string MinVersion { get; set; }
        public string MaxVersion { get; set; }
    }

    public class Profile
    {
        public string Name { get; set; }
        public ObservableCollection<Mod> Mods { get; set; } = new ObservableCollection<Mod>();

        public override string ToString()
        {
            return Name;
        }

        public string ExportToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        public static Profile ImportFromJson(string json)
        {
            return JsonConvert.DeserializeObject<Profile>(json);
        }
    }

    public class StringArrayToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string[] array)
            {
                return string.Join(", ", array);
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                return str.Split(new[] { ", " }, StringSplitOptions.None);
            }
            return value;
        }
    }

}

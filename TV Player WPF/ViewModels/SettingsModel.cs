using Newtonsoft.Json;
using System.IO;

namespace TV_Player.ViewModels
{
    public static class SettingsModel
    {
        private static readonly string AppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TVPlayer");
        private static readonly string SettingsFilePath = Path.Combine(AppDataFolder, "settings.json");

        public static string PlaylistURL { get; set; }
        public static bool StartFullScreen { get; set; } 
        public static bool StartFromLastScreen { get; set; } 
        public static string LastScreen { get; set; }
        public static GroupInfo Group { get; set; }
        public static M3UInfo Program { get; set; }
        public static string[] HiddenGroups { get; set; }

        public static void SaveSetttings()
        {
            // Create an anonymous object to hold the properties
            var dataToSerialize = new
            {
                PlaylistURL,
                StartFromLastScreen,
                StartFullScreen,
                LastScreen,
                Group,
                Program,
                HiddenGroups,
            };

            // Serialize the object to JSON
            string json = JsonConvert.SerializeObject(dataToSerialize, Formatting.Indented);

            if (!Directory.Exists(AppDataFolder))
                Directory.CreateDirectory(AppDataFolder);
            // Save the JSON to a file
            File.WriteAllText(SettingsFilePath, json);
        }

        public static void LoadSettings()
        {
            var loadedData = new
            {
                PlaylistURL = default(string),
                LastScreen = default(string),
                Group = default(GroupInfo),
                Program = default(M3UInfo),
                StartFromLastScreen = default(bool),
                StartFullScreen = default(bool),
                HiddenGroups = default(string[])
            };
            if (File.Exists(SettingsFilePath))
            {
                // Read the JSON content from the file
                string json = File.ReadAllText(SettingsFilePath);
                loadedData = JsonConvert.DeserializeAnonymousType(json, loadedData);
            }
            // Assign the values to the properties
            PlaylistURL = loadedData.PlaylistURL;
            LastScreen = loadedData.LastScreen;
            Group = loadedData.Group;
            Program = loadedData.Program;
            StartFromLastScreen = loadedData.StartFromLastScreen;
            StartFullScreen = loadedData.StartFullScreen;
            HiddenGroups = loadedData.HiddenGroups;
        }
    }
}

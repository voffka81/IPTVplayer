using Newtonsoft.Json;
using System.IO;

namespace TV_Player.ViewModels
{
    public static class SettingsModel
    {
        private const string _filePath = "settings.json";

        public static string PlaylistURL { get; set; }
        public static bool StartFullScreen { get; set; } 
        public static bool StartFromLastScreen { get; set; } 
        public static string LastScreen { get; set; }
        public static GroupInfo Group { get; set; }
        public static M3UInfo Program { get; set; }


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
                Program
            };

            // Serialize the object to JSON
            string json = JsonConvert.SerializeObject(dataToSerialize, Formatting.Indented);

            // Save the JSON to a file
            File.WriteAllText(_filePath, json);
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
                StartFullScreen = default(bool)
            };
            if (File.Exists(_filePath))
            {
                // Read the JSON content from the file
                string json = File.ReadAllText(_filePath);
                loadedData = JsonConvert.DeserializeAnonymousType(json, loadedData);
            }
            // Assign the values to the properties
            PlaylistURL = loadedData.PlaylistURL;
            LastScreen = loadedData.LastScreen;
            Group = loadedData.Group;
            Program = loadedData.Program;
            StartFromLastScreen = loadedData.StartFromLastScreen;
            StartFullScreen = loadedData.StartFullScreen;
        }
    }
}

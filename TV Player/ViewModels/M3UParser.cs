﻿using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Xml;

namespace TV_Player.MAUI
{
    public class ProgramInfo : ObservableViewModelBase
    {
        private string _title;
        private int _durationValue;

        public string Title { get => _title; set => SetProperty(ref _title, value); }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int DurationValue { get => _durationValue; set => SetProperty(ref _durationValue, value); }

    }

    public class ProgramGuide
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public List<ProgramInfo> Programs { get; set; } = new List<ProgramInfo>();
    }
    public static class M3UParser
    {

        public static async Task<List<ProgramGuide>> DownloadGuideFromWebAsync(string url)
        {
            List<ProgramGuide> epgChannels = new List<ProgramGuide>(); ;
            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage())
            {
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/text"));
                request.Method = HttpMethod.Get;
                request.RequestUri = new Uri(url);
                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();
                epgChannels = ParseEpg(responseBody);
            }
            return epgChannels;
        }

        private static List<ProgramGuide> ParseEpg(string epgData)
        {
            List<ProgramGuide> epgChannels = new List<ProgramGuide>();
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.IgnoreWhitespace = true;
            settings.IgnoreComments = true;

            using (XmlReader reader = XmlReader.Create(new System.IO.StringReader(epgData), settings))
            {
                try
                {
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element && reader.Name == "channel")
                        {
                            ProgramGuide channel = new ProgramGuide();
                            channel.Id = reader.GetAttribute("id");
                            reader.Read();
                            channel.DisplayName = reader.ReadElementContentAsString();

                            epgChannels.Add(channel);
                            continue;
                        }

                        if (reader.NodeType == XmlNodeType.Element && reader.Name == "programme")
                        {
                            ProgramInfo program = new ProgramInfo();

                            var id = reader.GetAttribute("channel");
                            var channel = epgChannels.FirstOrDefault(x => x.Id == id);
                            program.StartTime = DateTime.ParseExact(reader.GetAttribute("start"), "yyyyMMddHHmmss zzz", null);
                            program.EndTime = DateTime.ParseExact(reader.GetAttribute("stop"), "yyyyMMddHHmmss zzz", null);

                            reader.Read();
                            program.Title = reader.ReadElementContentAsString();


                            channel.Programs.Add(program);
                        }
                        else if (reader.NodeType == XmlNodeType.EndElement && reader.Name == "channel")
                        {
                            break;
                        }
                    }
                }
                catch{}
            }
            return epgChannels;
        }

        private static async Task<string> ReadFile(string url)
        {
            string responseBody;
            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage())
            {
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/text"));
                request.Method = HttpMethod.Get;
                request.RequestUri = new Uri(url);
                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                responseBody = await response.Content.ReadAsStringAsync();
            }
            return responseBody;
        }

        public static async Task<(List<M3UInfo> programList, string programGuide)> DownloadM3UFromWebAsync(string url)
        {
            var fileData=await ReadFile(url);
            // Parse M3U content
            return ParseM3UFromString(fileData);
        }
        private static string[] SplitStringBeforeSeparator(string input, string separator)
        {
            string[] parts = input.Split(separator);

            // Reconstruct the string until the separator is reached
            int separatorIndex = input.IndexOf(separator);
            if (separatorIndex != -1)
            {
                parts[0] = input.Substring(0, separatorIndex + 1);
                for (int i = 1; i < parts.Length; i++)
                {
                    parts[i] = separator + parts[i];
                }
            }

            return parts;
        }

        private static (List<M3UInfo> programList, string programGuide) ParseM3UFromString(string content)
        {
            List<M3UInfo> playlistItems = new List<M3UInfo>();
            string programGuideLink = string.Empty;
            try
            {
                var m3u = SplitStringBeforeSeparator(content, "#EXT");

                foreach (var line in m3u)
                {
                    if (line.StartsWith("#EXTINF:"))
                    {
                        if (TryParseM3ULine(line, out var m3uInfo))
                        {
                            playlistItems.Add(m3uInfo);
                        }
                    }
                    if (line.StartsWith("#EXTM3U"))
                    {
                        programGuideLink=ExtractXtvgUrl(line);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error reading M3U file: " + ex.Message);
            }

            return (playlistItems,programGuideLink);
        }

        private static bool TryParseM3ULine(string m3uLine, out M3UInfo? info)
        {
            info = null;
            string pattern = @"#EXTINF:\d+ CUID=""(?<CUID>.*?)"" number=""(?<Number>.*?)"" tvg-id=""(?<TvgID>.*?)"" tvg-name=""(?<TvgName>.*?)"".*?tvg-logo=""(?<Logo>.*?)"" group-title=""(?<GroupTitle>.*?)""[^,]*,(?<Name>.*)[^\r](?<URL>.*)$";
            Regex regex = new Regex(pattern, RegexOptions.IgnoreCase);

            Match match = regex.Match(m3uLine);
            if (match.Success)
            {
                info = new M3UInfo
                {
                    CUID = match.Groups["CUID"].Value,
                    Number = match.Groups["Number"].Value,
                    TvgID = match.Groups["TvgID"].Value,
                    TvgName = match.Groups["TvgName"].Value,
                    GroupTitle = match.Groups["GroupTitle"].Value,
                    Logo = match.Groups["Logo"].Value,
                    Name = match.Groups["Name"].Value,
                    Url = match.Groups["URL"].Value
                };
                return true;
            }

            return false;
        }

        private static string ExtractXtvgUrl(string m3uEntry)
        {
            // Define a regular expression pattern to match x-tvg-url attribute
            string pattern = @"x-tvg-url=""(.*?)""";

            // Use Regex.Match to find the first match
            Match match = Regex.Match(m3uEntry, pattern);

            // Check if a match is found and get the value from the capturing group
            if (match.Success && match.Groups.Count > 1)
            {
                return match.Groups[1].Value;
            }

            // Return null or an empty string if no match is found
            return string.Empty;
        }
    }
}

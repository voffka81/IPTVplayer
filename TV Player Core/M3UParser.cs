using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace TV_Player
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
        private static string GetWritableAppDataFolder()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrWhiteSpace(localAppData))
            {
                return localAppData;
            }

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrWhiteSpace(appData))
            {
                return appData;
            }

            return AppContext.BaseDirectory;
        }

        public static async Task DownloadGuideFromWebAsync(string name, string url)
        {
            var fileName = name + "_guide.xml";
            string programDataPath = GetWritableAppDataFolder();
            string filePath = Path.Combine(programDataPath, "TVPlayer", fileName);


            if (File.Exists(filePath))
            {
                DateTime creationTime = File.GetCreationTime(filePath);
                DateTime modificationTime = File.GetLastWriteTime(filePath);
                DateTime currentTime = DateTime.Now;

                if ((currentTime - creationTime).TotalDays < 3 || (currentTime - modificationTime).TotalDays < 3)
                {
                    return;
                }
            }
            ;
            var channelsContent = string.Empty;

            if (url.Contains(".gz"))
            {
                channelsContent = await DownloadGzip(url);
            }
            else
            {
                channelsContent = await DownloadXMLProgram(url);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            await File.WriteAllTextAsync(filePath, channelsContent);
        }

        private static async Task<string> DownloadXMLProgram(string url)
        {
            try
            {
                using (var client = new HttpClient())
                using (var request = new HttpRequestMessage())
                {
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/text"));
                    request.Method = HttpMethod.Get;
                    request.RequestUri = new Uri(url);
                    var response = await client.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync();
                }
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Network error downloading XML from {url}: {ex.Message}");
                throw;
            }
            catch (UriFormatException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Invalid URL: {url} - {ex.Message}");
                throw;
            }
        }

        public static async Task<string> DownloadGzip(string url)
        {
            try
            {
                using HttpClient httpClient = new HttpClient();
                byte[] compressedData = await httpClient.GetByteArrayAsync(url);

                using MemoryStream compressedStream = new MemoryStream(compressedData);
                using GZipStream decompressionStream = new GZipStream(compressedStream, CompressionMode.Decompress);
                using MemoryStream decompressedStream = new MemoryStream();
                await decompressionStream.CopyToAsync(decompressedStream);

                string xmlContent = Encoding.UTF8.GetString(decompressedStream.ToArray());
                return xmlContent;
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Network error downloading gzip from {url}: {ex.Message}");
                return null;
            }
            catch (InvalidOperationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Invalid gzip data: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error decompressing gzip: {ex.Message}");
                return null;
            }
        }

        public static async Task<ProgramGuide> ParseEpg(string groupName, string channelId)
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.IgnoreWhitespace = true;
            settings.IgnoreComments = true;
            settings.DtdProcessing = DtdProcessing.Parse;
            settings.Async = true;
            ProgramGuide channel = null;

            var fileName = groupName + "_guide.xml";
            string programDataPath = GetWritableAppDataFolder();
            string filePath = Path.Combine(programDataPath, "TVPlayer", fileName);

            if (!File.Exists(filePath))
            {
                System.Diagnostics.Debug.WriteLine($"EPG file not found: {filePath}");
                return null;
            }

            using (XmlReader reader = XmlReader.Create(filePath, settings))
            {
                try
                {
                    while (await reader.ReadAsync())
                    {
                        if (reader.NodeType == XmlNodeType.Element && reader.Name == "channel")
                        {
                            var id = reader.GetAttribute("id");
                            if (id == channelId)
                            {
                                channel = new ProgramGuide
                                {
                                    Id = id
                                };
                                reader.Read();
                                channel.DisplayName = reader.ReadElementContentAsString();
                            }
                            continue;
                        }

                        if (channel != null && reader.NodeType == XmlNodeType.Element && reader.Name == "programme")
                        {
                            ProgramInfo program = new ProgramInfo();

                            var id = reader.GetAttribute("channel");
                            if (id != channelId) continue;
                            
                            if (!DateTime.TryParseExact(reader.GetAttribute("start"), "yyyyMMddHHmmss zzz", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var startTime))
                                continue;
                            if (!DateTime.TryParseExact(reader.GetAttribute("stop"), "yyyyMMddHHmmss zzz", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var endTime))
                                continue;
                                
                            program.StartTime = startTime;
                            program.EndTime = endTime;

                            reader.Read();
                            program.Title = reader.ReadElementContentAsString();

                            channel.Programs.Add(program);
                        }
                    }
                }
                catch (XmlException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"XML parsing error in EPG file: {ex.Message}");
                }
                catch (FileNotFoundException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"EPG file not found: {ex.Message}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Unexpected error parsing EPG: {ex.Message}");
                }
            }
            return channel;
        }

        private static async Task<string> ReadFile(string url)
        {
            try
            {
                using var client = new HttpClient();
                using var request = new HttpRequestMessage();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/text"));
                request.Method = HttpMethod.Get;
                request.RequestUri = new Uri(url);
                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Network error reading file from {url}: {ex.Message}");
                throw;
            }
            catch (UriFormatException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Invalid URL: {url} - {ex.Message}");
                throw;
            }
        }

        public static async Task<(List<M3UInfo> programList, string programGuide)> DownloadM3UFromWebAsync(string url)
        {
            string fileData;
            if (Uri.IsWellFormedUriString(url, UriKind.Absolute))
            {
                fileData = await ReadFile(url);
            }
            else
            {
                fileData = File.ReadAllText(url);
            }

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
                if (string.IsNullOrWhiteSpace(content))
                {
                    System.Diagnostics.Debug.WriteLine("[M3UParser] M3U content is empty");
                    return (playlistItems, programGuideLink);
                }

                System.Diagnostics.Debug.WriteLine($"[M3UParser] Starting parse of M3U content ({content.Length} bytes)");
                var m3u = SplitStringBeforeSeparator(content, "#EXT");

                foreach (var line in m3u)
                {
                    if (line.StartsWith("#EXTINF:"))
                    {
                        if (TryParseM3ULine(line, out var m3uInfo))
                        {
                            if (!string.IsNullOrEmpty(m3uInfo?.Url))
                            {
                                playlistItems.Add(m3uInfo);
                                System.Diagnostics.Debug.WriteLine($"[M3UParser] Parsed: {m3uInfo.Name} -> group='{m3uInfo.GroupTitle}'");
                            }
                        }
                    }
                    if (line.StartsWith("#EXTM3U"))
                    {
                        programGuideLink = ExtractXtvgUrl(line);
                    }
                }
                
                var groupSummary = playlistItems.GroupBy(p => p.GroupTitle).Select(g => $"{g.Key}({g.Count()})").ToList();
                System.Diagnostics.Debug.WriteLine($"[M3UParser] Parse complete: {playlistItems.Count} programs in {groupSummary.Count} groups: {string.Join(", ", groupSummary)}");
            }
            catch (ArgumentException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Invalid M3U format: {ex.Message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing M3U file: {ex.Message}");
            }

            return (playlistItems, programGuideLink);
        }

        private static bool TryParseM3ULine(string m3uLine, out M3UInfo? info)
        {
            info = null;
            string pattern = @"#EXTINF:(-?\d+)\s*?(?:timeshift=""(?<Timeshift>.*?)""\s+)?(?:catchup-days=""(?<CatchupDays>.*?)""\s+)?(?:catchup-type=""(?<CatchupType>.*?)""\s+)?(?:CUID=""(?<CUID>.*?)""\s+)?(?:number=""(?<Number>.*?)""\s+)?(?:tvg-id=""(?<TvgID>.*?)""\s+)?(?:tvg-name=""(?<TvgName>.*?)""\s+)?(?:group-title=""(?<GroupTitle>.*?)""\s+)?(?:tvg-logo=""(?<Logo>.*?)""\s*)?(?:group-title=""(?<GroupTitle>.*?)"")?(?:,(?<Name>.*?)\s*\r?\n(?<URL>.*))";
            //string pattern = @"#EXTINF:(-?\d+)\s+?(?:timeshift=""(?<Timeshift>.*?)""\s+)?(?:catchup-days=""(?<CatchupDays>.*?)""\s+)?(?:catchup-type=""(?<CatchupType>.*?)""\s+)?(?:CUID=""(?<CUID>.*?)""\s+)?(?:number=""(?<Number>.*?)""\s+)?(?:tvg-id=""(?<TvgID>.*?)""\s+)?(?:tvg-name=""(?<TvgName>.*?)""\s+)?(?:group-title=""(?<GroupTitle>.*?)""\s+)?(?:tvg-logo=""(?<Logo>.*?)"")?,(?<Name>.*?)\s*\r?\n(?<URL>.*)";
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
                    GroupTitle = string.IsNullOrEmpty(match.Groups["GroupTitle"].Value) ? "undefined" : match.Groups["GroupTitle"].Value,
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
            string pattern = @"(x-tvg-url=""(.*?)"")?(url-tvg=""(.*?)"")";

            // Use Regex.Match to find the first match
            Match match = Regex.Match(m3uEntry, pattern);

            // Check if a match is found and get the value from the capturing group
            if (match.Success && match.Groups.Count > 1)
            {
                return match.Groups[4].Value;
            }

            // Return null or an empty string if no match is found
            return string.Empty;
        }
    }
}

using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

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

        public static async Task DownloadGuideFromWebAsync(string name, string url, string? guideKey = null)
        {
            var suffix = string.IsNullOrWhiteSpace(guideKey) ? string.Empty : "_" + guideKey;
            var fileName = name + "_guide" + suffix + ".xml";
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

        public static Task<ProgramGuide> ParseEpg(string groupName, string channelId)
            => ParseEpg(groupName, channelId, Array.Empty<string>());

        public static async Task<ProgramGuide> ParseEpg(string groupName, string channelId, IReadOnlyCollection<string> channelNames, string? guideKey = null)
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.IgnoreWhitespace = true;
            settings.IgnoreComments = true;
            settings.DtdProcessing = DtdProcessing.Prohibit;
            settings.XmlResolver = null;
            settings.Async = true;

            var suffix = string.IsNullOrWhiteSpace(guideKey) ? string.Empty : "_" + guideKey;
            var fileName = groupName + "_guide" + suffix + ".xml";
            string programDataPath = GetWritableAppDataFolder();
            string filePath = Path.Combine(programDataPath, "TVPlayer", fileName);

            if (!File.Exists(filePath))
            {
                System.Diagnostics.Debug.WriteLine($"EPG file not found: {filePath}");
                return null;
            }

            try
            {
                using var reader = XmlReader.Create(filePath, settings);
                var document = await XDocument.LoadAsync(reader, LoadOptions.None, CancellationToken.None);
                var channels = document.Root?.Elements("channel") ?? Enumerable.Empty<XElement>();
                var requestedNames = channelNames
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(NormalizeChannelName)
                    .Where(value => value.Length > 0)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var matchedChannel = channels.FirstOrDefault(item =>
                    string.Equals((string?)item.Attribute("id"), channelId, StringComparison.Ordinal));
                matchedChannel ??= channels.FirstOrDefault(item => item.Elements("display-name")
                    .Any(displayName => requestedNames.Contains(NormalizeChannelName(displayName.Value))));
                if (matchedChannel == null)
                    return null;

                var matchedId = (string?)matchedChannel.Attribute("id") ?? string.Empty;
                var channel = new ProgramGuide
                {
                    Id = matchedId,
                    DisplayName = matchedChannel.Elements("display-name").FirstOrDefault()?.Value ?? string.Empty
                };

                foreach (var item in document.Root?.Elements("programme") ?? Enumerable.Empty<XElement>())
                {
                    if (!string.Equals((string?)item.Attribute("channel"), matchedId, StringComparison.Ordinal) ||
                        !TryParseXmlTvTimestamp((string?)item.Attribute("start"), out var startTime) ||
                        !TryParseXmlTvTimestamp((string?)item.Attribute("stop"), out var endTime))
                        continue;

                    channel.Programs.Add(new ProgramInfo
                    {
                        StartTime = startTime,
                        EndTime = endTime,
                        Title = item.Elements("title").FirstOrDefault()?.Value ?? string.Empty
                    });
                }

                channel.Programs = channel.Programs.OrderBy(program => program.StartTime).ToList();
                return channel;
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

            return null;
        }

        private static string NormalizeChannelName(string value)
            => new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

        public static (string url, string? cacheKey) SelectGuideUrl(string guideUrls, M3UInfo channel)
        {
            var urls = guideUrls.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (urls.Length == 0)
                return (string.Empty, null);
            if (urls.Length == 1)
                return (urls[0], null);

            var country = channel.TvgCountry?.Trim();
            if (string.IsNullOrWhiteSpace(country) && !string.IsNullOrWhiteSpace(channel.TvgID))
                country = channel.TvgID.Split('.').LastOrDefault();

            if (!string.IsNullOrWhiteSpace(country) && Regex.IsMatch(country, @"^[A-Za-z]{2,3}$"))
            {
                var countryUrl = urls.FirstOrDefault(url =>
                    url.Contains($"_{country}1.xml", StringComparison.OrdinalIgnoreCase));
                if (countryUrl != null)
                    return (countryUrl, country.ToUpperInvariant());
            }

            System.Diagnostics.Debug.WriteLine($"No country-specific XMLTV URL found for '{channel.Name}' ({channel.TvgCountry}, {channel.TvgID}).");
            return (string.Empty, null);
        }

        private static bool TryParseXmlTvTimestamp(string? value, out DateTime localTime)
        {
            localTime = default;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var timestamp = Regex.Replace(value.Trim(), @"([+-]\d{2})(\d{2})$", "$1:$2");
            var formatsWithOffset = new[] { "yyyyMMddHHmmss zzz", "yyyyMMddHHmm zzz" };
            if (DateTimeOffset.TryParseExact(timestamp, formatsWithOffset,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var dateWithOffset))
            {
                localTime = dateWithOffset.LocalDateTime;
                return true;
            }

            var localFormats = new[] { "yyyyMMddHHmmss", "yyyyMMddHHmm" };
            return DateTime.TryParseExact(timestamp, localFormats,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeLocal, out localTime);
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading file from {url}: {ex.Message}");
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
            var lines = m3uLine.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n');
            if (lines.Length < 2 || !lines[0].StartsWith("#EXTINF:", StringComparison.OrdinalIgnoreCase))
                return false;

            var header = lines[0];
            var commaIndex = FindUnquotedComma(header);
            var attributeStart = header.IndexOf(':') + 1;
            if (commaIndex < attributeStart || attributeStart <= 0)
                return false;

            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            const string attributePattern = @"(?<key>[\w-]+)\s*=\s*(?:""(?<double>[^""]*)""|'(?<single>[^']*)'|(?<bare>[^\s]+))";
            foreach (Match match in Regex.Matches(header[attributeStart..commaIndex], attributePattern))
            {
                var value = match.Groups["double"].Success ? match.Groups["double"].Value
                    : match.Groups["single"].Success ? match.Groups["single"].Value
                    : match.Groups["bare"].Value;
                attributes[match.Groups["key"].Value] = value;
            }

            var streamUrl = lines.Skip(1)
                .Select(line => line.Trim())
                .FirstOrDefault(line => line.Length > 0 && !line.StartsWith('#'));
            if (string.IsNullOrWhiteSpace(streamUrl))
                return false;

            var groupTitle = GetAttribute("group-title");
            info = new M3UInfo
            {
                CUID = GetAttribute("CUID"),
                Number = GetAttribute("number"),
                TvgID = GetAttribute("tvg-id"),
                TvgName = GetAttribute("tvg-name"),
                TvgCountry = GetAttribute("tvg-country"),
                GroupTitle = string.IsNullOrEmpty(groupTitle) ? "undefined" : groupTitle,
                Logo = GetAttribute("tvg-logo"),
                Name = header[(commaIndex + 1)..].Trim(),
                Url = streamUrl
            };
            return true;

            string GetAttribute(string key) => attributes.TryGetValue(key, out var value) ? value : string.Empty;
        }

        private static int FindUnquotedComma(string value)
        {
            var inSingleQuote = false;
            var inDoubleQuote = false;
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (character == '\'' && !inDoubleQuote)
                    inSingleQuote = !inSingleQuote;
                else if (character == '"' && !inSingleQuote)
                    inDoubleQuote = !inDoubleQuote;
                else if (character == ',' && !inSingleQuote && !inDoubleQuote)
                    return index;
            }

            return -1;
        }

        private static string ExtractXtvgUrl(string m3uEntry)
        {
            const string pattern = @"(?:x-tvg-url|url-tvg)\s*=\s*(?:""(?<double>[^""]*)""|'(?<single>[^']*)'|(?<bare>[^\s]+))";
            foreach (Match match in Regex.Matches(m3uEntry, pattern, RegexOptions.IgnoreCase))
            {
                var value = match.Groups["double"].Success ? match.Groups["double"].Value
                    : match.Groups["single"].Success ? match.Groups["single"].Value
                    : match.Groups["bare"].Value;
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return string.Empty;
        }
    }
}

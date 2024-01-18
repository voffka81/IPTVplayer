using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace TV_Player
{
    public static class M3UParser
    {
        public static async Task<List<M3UInfo>> DownloadM3UFromWebAsync(string url)
        {
            List<M3UInfo> playlistItems = new List<M3UInfo>();

            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage())
            {
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/text"));
                request.Method = HttpMethod.Get;
                request.RequestUri = new Uri(url);
                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                // Parse M3U content
                playlistItems = ParseM3UFromString(responseBody);
            }
            return playlistItems;
        }

        static string[] SplitStringBeforeSeparator(string input, string separator)
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

        static List<M3UInfo> ParseM3UFromString(string content)
        {
            List<M3UInfo> playlistItems = new List<M3UInfo>();

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
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error reading M3U file: " + ex.Message);
            }

            return playlistItems;
        }


        static bool TryParseM3ULine(string m3uLine, out M3UInfo? info)
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
    }
}

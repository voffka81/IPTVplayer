using TV_Player.Plugins;
using TV_Player.Providers;
using System.Reactive;
using System.Reactive.Subjects;

namespace TV_Player
{
    public class ProgramsData : IDisposable
    {
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly ReplaySubject<List<M3UInfo>> programsSubject = new ReplaySubject<List<M3UInfo>>();
        private readonly ReplaySubject<List<GroupInfo>> groupsSubject = new ReplaySubject<List<GroupInfo>>();
        private readonly ReplaySubject<Unit> programGuideSubject = new ReplaySubject<Unit>();
        public IObservable<List<M3UInfo>> AllPrograms => programsSubject;
        public IObservable<List<GroupInfo>> GroupsInformation => groupsSubject;
        public IObservable<Unit> ProgramGuideInfo => programGuideSubject;

        private readonly string _programName;
        public IChannelProvider Provider { get; }

        public ProgramsData(string name,string playlistURL)
        {
            _programName = name;
            Provider = new M3UChannelProvider("m3u:" + name, name, playlistURL);
            Task.Run(() => GetPrograms(name,playlistURL), _cts.Token);
        }

        private async Task GetPrograms(string name,string m3uLink)
        {
            if (_cts.IsCancellationRequested) return;
            System.Diagnostics.Debug.WriteLine($"[ProgramsData] Starting download of: {m3uLink}");
            //string m3uLink = "http://pl.da-tv.vip/a71e77fa/835b3216/tv.m3u";
            try
            {
                // if link is a well-formed absolute URI load from web, otherwise treat as a local file path
                // for relative paths try resolving against the application's base directory
                var result = (programList: new List<M3UInfo>(), programGuide: string.Empty);
                if (Uri.IsWellFormedUriString(m3uLink, UriKind.Absolute))
                {
                    System.Diagnostics.Debug.WriteLine("[ProgramsData] Detected URL, downloading from web");
                    result = await ((M3UChannelProvider)Provider).LoadPlaylistAsync(_cts.Token);
                }
                else
                {
                    // try as provided path first
                    var candidate = m3uLink;
                    if (!Path.IsPathRooted(candidate))
                    {
                        candidate = Path.Combine(AppContext.BaseDirectory ?? string.Empty, candidate);
                    }

                    if (File.Exists(candidate))
                    {
                        System.Diagnostics.Debug.WriteLine($"[ProgramsData] Detected local file, loading from disk: {candidate}");
                        result = await ((M3UChannelProvider)Provider).LoadPlaylistAsync(_cts.Token);
                    }
                    else
                    {
                        throw new FileNotFoundException($"M3U file not found: {m3uLink}");
                    }
                }
                System.Diagnostics.Debug.WriteLine($"[ProgramsData] Downloaded {result.programList.Count} programs");

                programsSubject.OnNext(result.programList);

                var groupping = result.programList.GroupBy(item => item.GroupTitle)
                                   .Select(group => new GroupInfo() { Name = group.Key, Count = group.Count() })
                                   .OrderBy(g => g.Name)
                                   .ToList();
                System.Diagnostics.Debug.WriteLine($"[ProgramsData] Publishing {groupping.Count} groups: {string.Join(", ", groupping.Select(g => $"{g.Name}({g.Count})"))}");
                if (groupping.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("[ProgramsData] WARNING: No groups found! Check if programs have 'group-title' metadata.");
                }
                groupsSubject.OnNext(groupping);
                
                await Task.Run(() => GetProgramGuide(name, result.programGuide), _cts.Token);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProgramsData] ERROR downloading programs: {ex.Message}");
                throw;
            }
        }

        public Task<ProgramGuide> GetGuideByProgram(string channelID)
        {
            return M3UParser.ParseEpg(_programName,channelID);
        }

        private async Task GetProgramGuide(string name, string guideLink)
        {
            if (_cts.IsCancellationRequested) return;
            //guideLink = "http://epg.da-tv.vip/107-light.xml";
            await M3UParser.DownloadGuideFromWebAsync(name,guideLink);
            programGuideSubject.OnNext(Unit.Default);
        }
        public void Dispose()
        {
            try
            {
                _cts.Cancel();
            }
            catch { }
            try
            {
                _cts.Dispose();
            }
            catch { }
            try
            {
                programGuideSubject.OnCompleted();
                programGuideSubject.Dispose();
            }
            catch { }

            try
            {
                programsSubject.OnCompleted();
                programsSubject.Dispose();
            }
            catch { }

            try
            {
                groupsSubject.OnCompleted();
                groupsSubject.Dispose();
            }
            catch { }
        }
        
    }
}

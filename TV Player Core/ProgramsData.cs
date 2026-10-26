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
        private string _programGuideLink = string.Empty;
        public IChannelProvider Provider { get; }

        public ProgramsData(string name,string playlistURL)
        {
            _programName = name;
            Provider = new M3UChannelProvider("m3u:" + name, name, playlistURL);
            Task.Run(() => GetPrograms(name, playlistURL), _cts.Token);
        }

        public ProgramsData(string name, IChannelProvider provider)
        {
            _programName = name;
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));
            Task.Run(() => GetPrograms(name, string.Empty), _cts.Token);
        }

        private async Task GetPrograms(string name,string m3uLink)
        {
            if (_cts.IsCancellationRequested) return;
            System.Diagnostics.Debug.WriteLine($"[ProgramsData] Starting download of: {m3uLink}");
            //string m3uLink = "http://pl.da-tv.vip/a71e77fa/835b3216/tv.m3u";
            try
            {
                List<M3UInfo> programs;
                string guideLink = string.Empty;
                if (Provider is M3UChannelProvider m3uProvider)
                {
                    var result = await m3uProvider.LoadPlaylistAsync(_cts.Token);
                    programs = result.programList;
                    guideLink = result.programGuide;
                }
                else
                {
                    var channels = await Provider.GetChannelsAsync(_cts.Token);
                    programs = channels.Select(channel => new M3UInfo
                    {
                        ProviderId = Provider.Id,
                        ChannelId = channel.Id,
                        Name = channel.Name,
                        GroupTitle = channel.GroupTitle,
                        Logo = channel.Logo ?? string.Empty,
                        TvgID = channel.EpgId ?? string.Empty,
                        Url = string.Empty
                    }).ToList();
                    if (Provider is IProgramGuideSource guideSource)
                        guideLink = guideSource.ProgramGuideUrl ?? string.Empty;
                }
                _programGuideLink = guideLink;
                _cts.Token.ThrowIfCancellationRequested();
                System.Diagnostics.Debug.WriteLine($"[ProgramsData] Downloaded {programs.Count} programs");

                programsSubject.OnNext(programs);

                var groupping = programs.GroupBy(item => item.GroupTitle)
                                   .Select(group => new GroupInfo() { Name = group.Key, Count = group.Count() })
                                   .OrderBy(g => g.Name)
                                   .ToList();
                System.Diagnostics.Debug.WriteLine($"[ProgramsData] Publishing {groupping.Count} groups: {string.Join(", ", groupping.Select(g => $"{g.Name}({g.Count})"))}");
                if (groupping.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("[ProgramsData] WARNING: No groups found! Check if programs have 'group-title' metadata.");
                }
                groupsSubject.OnNext(groupping);
                
                if (!string.IsNullOrWhiteSpace(guideLink))
                    programGuideSubject.OnNext(Unit.Default);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProgramsData] ERROR downloading programs: {ex.Message}");
                throw;
            }
        }

        public Task<ProgramGuide> GetGuideByProgram(string channelID)
        {
            return GetGuideByProgram(new M3UInfo { TvgID = channelID, TvgName = string.Empty, Name = string.Empty });
        }

        public async Task<ProgramGuide> GetGuideByProgram(M3UInfo channel)
        {
            if (_cts.IsCancellationRequested || string.IsNullOrWhiteSpace(_programGuideLink))
                return null;

            var (guideUrl, cacheKey) = M3UParser.SelectGuideUrl(_programGuideLink, channel);
            if (string.IsNullOrWhiteSpace(guideUrl))
                return null;

            await M3UParser.DownloadGuideFromWebAsync(_programName, guideUrl, cacheKey);
            var names = new[] { channel.TvgName, channel.Name };
            return await M3UParser.ParseEpg(_programName, channel.TvgID ?? string.Empty, names, cacheKey);
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
            if (Provider is IDisposable providerDisposable)
            {
                try { providerDisposable.Dispose(); } catch { }
            }
        }
        
    }
}

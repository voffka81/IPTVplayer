using System.Reactive;
using System.Reactive.Subjects;

namespace TV_Player
{
    public class ProgramsData
    {
        private readonly ReplaySubject<List<M3UInfo>> programsSubject = new ReplaySubject<List<M3UInfo>>();
        private readonly ReplaySubject<List<GroupInfo>> groupsSubject = new ReplaySubject<List<GroupInfo>>();
        private readonly ReplaySubject<Unit> programGuideSubject = new ReplaySubject<Unit>();
        public IObservable<List<M3UInfo>> AllPrograms => programsSubject;
        public IObservable<List<GroupInfo>> GroupsInformation => groupsSubject;

        public IObservable<Unit> ProgramGuideInfo => programGuideSubject;
        public ProgramsData(string name,string playlistURL)
        {
            Task.Run(() => GetPrograms(name,playlistURL));
        }

        private async Task GetPrograms(string name,string m3uLink)
        {
            //string m3uLink = "http://pl.da-tv.vip/a71e77fa/835b3216/tv.m3u";
            var result = await M3UParser.DownloadM3UFromWebAsync(m3uLink);

            programsSubject.OnNext(result.programList);

            var groupping = result.programList.GroupBy(item => item.GroupTitle)
                               .Select(group => new GroupInfo() { Name = group.Key, Count = group.Count() })
                               .ToList();
            groupsSubject.OnNext(groupping);

            await Task.Run(() => GetProgramGuide(name,result.programGuide));
        }

        public Task<ProgramGuide> GetGuideByProgram(string channelID)
        {
            return M3UParser.ParseEpg(channelID);
        }

        private async Task GetProgramGuide(string name, string guideLink)
        {
            //guideLink = "http://epg.da-tv.vip/107-light.xml";
            await M3UParser.DownloadGuideFromWebAsync(name,guideLink);
            programGuideSubject.OnNext(Unit.Default);
        }
        
    }
}

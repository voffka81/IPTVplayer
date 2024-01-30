using System.Reactive.Subjects;

namespace TV_Player
{
    public class ProgramsData
    {
        private readonly ReplaySubject<List<M3UInfo>> programsSubject = new ReplaySubject<List<M3UInfo>>();
        private readonly ReplaySubject<List<GroupInfo>> groupsSubject = new ReplaySubject<List<GroupInfo>>();
        private readonly ReplaySubject<List<ProgramGuide>> programGuideSubject = new ReplaySubject<List<ProgramGuide>>();
        public IObservable<List<M3UInfo>> AllPrograms => programsSubject;
        public IObservable<List<GroupInfo>> GroupsInformation => groupsSubject;

        public IObservable<List<ProgramGuide>> ProgramGuideInfo => programGuideSubject;
        public ProgramsData()
        {            
        }

        private async Task GetPrograms(string m3uLink)
        {
            //string m3uLink = "http://pl.da-tv.vip/a71e77fa/835b3216/tv.m3u";
            var result = await M3UParser.DownloadM3UFromWebAsync(m3uLink);
            
            programsSubject.OnNext(result.programList);

            var groupping = result.programList.GroupBy(item => item.GroupTitle)
                               .Select(group => new GroupInfo() { Name = group.Key, Count = group.Count() })
                               .ToList();
            groupsSubject.OnNext(groupping);

            await Task.Run(() => GetProgramGuide(result.programGuide));
        }

        private async Task GetProgramGuide(string guideLink)
        {
            //string guideLink = "http://epg.da-tv.vip/107-light.xml";
            var programGuide = await M3UParser.DownloadGuideFromWebAsync(guideLink);
            programGuideSubject.OnNext(programGuide);
        }

        internal void GetData(string playlistURL)
        {
            Task.Run(() => GetPrograms(playlistURL));
        }
    }
}

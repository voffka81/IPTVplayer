using System.Reactive.Subjects;

namespace TV_Player
{
    public class ProgramsData
    {
        private static ProgramsData _instance;
        public static ProgramsData Instance
        {
            get
            {
                _instance ??= new ProgramsData();
                return _instance;
            }
        }

        private readonly ReplaySubject<List<M3UInfo>> programsSubject = new ReplaySubject<List<M3UInfo>>();
        private readonly ReplaySubject<List<GroupInfo>> groupsSubject = new ReplaySubject<List<GroupInfo>>();
        public IObservable<List<M3UInfo>> AllPrograms => programsSubject;
        public IObservable<List<GroupInfo>> GroupsInformation => groupsSubject;

        private ProgramsData()
        {
            _=Initialize();
        }
        private async Task Initialize()
        {
            string m3uLink = "http://pl.da-tv.vip/a71e77fa/835b3216/tv.m3u";
            var programs = await M3UParser.DownloadM3UFromWebAsync(m3uLink);

            programsSubject.OnNext(programs);

            var groupping = programs.GroupBy(item => item.GroupTitle)
                               .Select(group => new GroupInfo() { Name = group.Key, Count = group.Count() })
                               .ToList();
            groupsSubject.OnNext(groupping);
        }
    }
}

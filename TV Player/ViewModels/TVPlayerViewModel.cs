
namespace TV_Player.MAUI
{
    public class TVPlayerViewModel
    {
        public ProgramsData PlaylistData { get; private set; }

        public Action ButtonBackAction { get; set; }

        private static TVPlayerViewModel _instance;
        public static TVPlayerViewModel Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new TVPlayerViewModel();
                return _instance;
            }
        }

        public TVPlayerViewModel()
        {
            PlaylistData = new ProgramsData();
            PlaylistData.GetData("http://pl.da-tv.vip/a71e77fa/835b3216/tv.m3u");
        }

    }
}


namespace TV_Player.MAUI
{
    public class TVPlayerViewModel
    {
        public ProgramsData PlaylistData { get; private set; }

        public Action ButtonBackAction { get; set; }

        private static readonly Lazy<TVPlayerViewModel> _instance = 
            new Lazy<TVPlayerViewModel>(
                () => new TVPlayerViewModel(), 
                LazyThreadSafetyMode.ExecutionAndPublication);
        
        public static TVPlayerViewModel Instance => _instance.Value;

        public TVPlayerViewModel()
        {
            PlaylistData = new ProgramsData();
            
            // Load settings - can be overridden with custom configuration
            var settings = PlaylistSettings.Default;
            PlaylistData.GetData(settings.M3UUrl);
        }

    }
}

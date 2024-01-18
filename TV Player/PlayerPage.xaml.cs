namespace TV_Player.MAUI;

public partial class PlayerPage : ContentPage
{
	public PlayerPage()
	{
        this.BindingContext = this;
        InitializeComponent();
	}

    private void ContentPage_Loaded(object sender, EventArgs e)
    {
    }
}
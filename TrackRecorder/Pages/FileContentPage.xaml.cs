namespace TrackRecorder.Pages;

public partial class FileContentPage : ContentPage
{
    public FileContentPage(string content)
    {
        InitializeComponent();
        ContentEditor.Text = content;
    }

    private async void OnCloseClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}
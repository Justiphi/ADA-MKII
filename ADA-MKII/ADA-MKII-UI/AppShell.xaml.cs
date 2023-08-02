using ADA_MKII_UI.Pages;

namespace ADA_MKII_UI
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute("Settings", typeof(Settings));
            Routing.RegisterRoute("MainPage", typeof(MainPage));
        }
    }
}
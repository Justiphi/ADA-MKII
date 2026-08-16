using ADA_MKII_UI.Models;
using ADA_MKII_UI.PageModels;

namespace ADA_MKII_UI.Pages
{
    public partial class MainPage : ContentPage
    {
        public MainPage(MainPageModel model)
        {
            InitializeComponent();
            BindingContext = model;
        }
    }
}
using Avalonia.Controls;
using Avalonia.Interactivity;
using InstogramApp.ViewModels;

namespace InstogramApp.Views;

public partial class ExploreView : UserControl
{
    public ExploreView() => InitializeComponent();
    ExploreViewModel VM => (ExploreViewModel)DataContext!;
    void DoRefresh(object? s, RoutedEventArgs e) => VM.RefreshCommand.Execute(null);
}

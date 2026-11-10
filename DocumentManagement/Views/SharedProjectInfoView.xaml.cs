using System.Windows.Controls;

namespace DocumentManagement.Views;

public partial class SharedProjectInfoView : System.Windows.Controls.UserControl
{
    public SharedProjectInfoView()
    {
        InitializeComponent();
    }

    private void RootStack_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
    {
        ApplyResponsiveLayout(e.NewSize.Width);
    }

    private void ApplyResponsiveLayout(double width)
    {
        var compact = width < 1180;
        var narrow = width < 1180;

        Grid.SetRow(ProjectInfoCard, 0);
        Grid.SetColumn(ProjectInfoCard, 0);
        Grid.SetRow(ContractInfoCard, compact ? 1 : 0);
        Grid.SetColumn(ContractInfoCard, compact ? 0 : 1);
        Grid.SetColumnSpan(ProjectInfoCard, compact ? 2 : 1);
        Grid.SetColumnSpan(ContractInfoCard, compact ? 2 : 1);
        ProjectInfoCard.Margin = compact ? new System.Windows.Thickness(0, 0, 0, 12) : new System.Windows.Thickness(0, 0, 12, 0);
        ContractInfoCard.Margin = compact ? new System.Windows.Thickness(0) : new System.Windows.Thickness(12, 0, 0, 0);

        if (narrow)
        {
            PlaceRepresentativeCard(TvgsCard, 0);
            PlaceRepresentativeCard(GiaoThauCard, 1);
            PlaceRepresentativeCard(NhanThauCard, 2);
        }
        else
        {
            Grid.SetRow(TvgsCard, 0);
            Grid.SetColumn(TvgsCard, 0);
            Grid.SetColumnSpan(TvgsCard, 1);
            TvgsCard.Margin = new System.Windows.Thickness(0, 0, 8, 0);

            Grid.SetRow(GiaoThauCard, 0);
            Grid.SetColumn(GiaoThauCard, 1);
            Grid.SetColumnSpan(GiaoThauCard, 1);
            GiaoThauCard.Margin = new System.Windows.Thickness(8, 0, 8, 0);

            Grid.SetRow(NhanThauCard, 0);
            Grid.SetColumn(NhanThauCard, 2);
            Grid.SetColumnSpan(NhanThauCard, 1);
            NhanThauCard.Margin = new System.Windows.Thickness(8, 0, 0, 0);
        }
    }

    private static void PlaceRepresentativeCard(System.Windows.FrameworkElement card, int row)
    {
        Grid.SetRow(card, row);
        Grid.SetColumn(card, 0);
        Grid.SetColumnSpan(card, 3);
        card.Margin = new System.Windows.Thickness(0, 0, 0, row == 2 ? 0 : 12);
    }
}

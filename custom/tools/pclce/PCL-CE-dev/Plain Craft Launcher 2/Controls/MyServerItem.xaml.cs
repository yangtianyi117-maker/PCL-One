using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PCL;

/// <summary>服务器目录里的一行，视觉与模组条目（MyCompItem）保持同一套语言。</summary>
public partial class MyServerItem : Grid
{
    public MyServerItem()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => ApplyIcon();
    }

    private void ApplyIcon()
    {
        if (DataContext is not ServerItemViewModel row) return;
        var icon = row.Entry.Icon?.Trim();
        if (string.IsNullOrWhiteSpace(icon)) return;

        try
        {
            PathLogo.Source = icon;
        }
        catch (Exception)
        {
            // 图标拉不到就保留默认图标
        }
    }

    private void Item_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ServerItemViewModel row) row.Join();
    }

    private void Item_MouseEnter(object sender, MouseEventArgs e) => PanButtons.Opacity = 1;

    private void Item_MouseLeave(object sender, MouseEventArgs e) => PanButtons.Opacity = 0;

    private void BtnJoin_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ServerItemViewModel row) row.Join();
        e.Handled = true;
    }

    private void BtnCopy_Click(object sender, EventArgs e)
    {
        if (DataContext is ServerItemViewModel row) row.CopyBedrock();
        if (e is RoutedEventArgs routed) routed.Handled = true;
    }
}
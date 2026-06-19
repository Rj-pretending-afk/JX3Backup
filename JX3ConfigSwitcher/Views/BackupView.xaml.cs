using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using JX3ConfigSwitcher.Models;
using JX3ConfigSwitcher.ViewModels;

namespace JX3ConfigSwitcher.Views;

public partial class BackupView : UserControl
{
    private Point _dragStartPoint;

    public BackupView()
    {
        InitializeComponent();
    }

    private void SlotCard_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private void SlotCard_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || sender is not FrameworkElement element)
        {
            return;
        }

        var current = e.GetPosition(null);
        if (Math.Abs(current.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(current.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        if (element.DataContext is SlotViewModel { HasData: true } slot)
        {
            AnimateDragSource(element, isDragging: true);
            try
            {
                DragDrop.DoDragDrop(element, slot.Number, DragDropEffects.Copy);
            }
            finally
            {
                AnimateDragSource(element, isDragging: false);
            }
        }
    }

    private void RoleCard_DragEnter(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement element && e.Data.GetDataPresent(typeof(int)))
        {
            AnimateDropTarget(element, isActive: true);
        }

        RoleCard_DragOver(sender, e);
    }

    private void RoleCard_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(int)) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void RoleCard_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            AnimateDropTarget(element, isActive: false);
        }

        e.Handled = true;
    }

    private async void RoleCard_Drop(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            AnimateDropTarget(element, isActive: false);
        }

        if (DataContext is not MainViewModel viewModel
            || sender is not FrameworkElement { DataContext: CharacterConfig target }
            || e.Data.GetData(typeof(int)) is not int slotNumber)
        {
            return;
        }

        await viewModel.CoverSlotToCharacterAsync(slotNumber, target);
    }

    private static void AnimateDragSource(FrameworkElement element, bool isDragging)
    {
        EnsureScaleTransform(element);
        var scale = (ScaleTransform)element.RenderTransform;
        var toScale = isDragging ? 0.94 : 1.0;
        var toOpacity = isDragging ? 0.68 : 1.0;

        AnimateDouble(scale, ScaleTransform.ScaleXProperty, toScale, 120);
        AnimateDouble(scale, ScaleTransform.ScaleYProperty, toScale, 120);
        AnimateDouble(element, OpacityProperty, toOpacity, 120);
    }

    private void AnimateDropTarget(FrameworkElement element, bool isActive)
    {
        EnsureScaleTransform(element);
        var scale = (ScaleTransform)element.RenderTransform;
        var toScale = isActive ? 1.025 : 1.0;

        AnimateDouble(scale, ScaleTransform.ScaleXProperty, toScale, 140);
        AnimateDouble(scale, ScaleTransform.ScaleYProperty, toScale, 140);

        if (element is Border border)
        {
            border.BorderBrush = isActive
                ? (Brush)FindResource("AccentBrush")
                : (Brush)FindResource("LineBrush");
            border.Background = isActive
                ? (Brush)FindResource("AccentSoftBrush")
                : (Brush)FindResource("InputBrush");
        }
    }

    private static void EnsureScaleTransform(FrameworkElement element)
    {
        if (element.RenderTransform is ScaleTransform)
        {
            return;
        }

        element.RenderTransformOrigin = new Point(0.5, 0.5);
        element.RenderTransform = new ScaleTransform(1, 1);
    }

    private static void AnimateDouble(IAnimatable target, DependencyProperty property, double to, int milliseconds)
    {
        target.BeginAnimation(
            property,
            new DoubleAnimation(to, TimeSpan.FromMilliseconds(milliseconds))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.HoldEnd
            });
    }
}

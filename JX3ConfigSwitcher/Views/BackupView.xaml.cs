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

    public void ApplyAccent(Color accent)
    {
        var shellBackground = Color.FromRgb(17, 24, 33);
        var panelBase = Color.FromRgb(26, 35, 45);
        var panelAltBase = Color.FromRgb(21, 29, 38);
        var inputBase = Color.FromRgb(16, 23, 32);
        var softAccent = Blend(accent, Color.FromRgb(24, 56, 74), 0.72);
        var panel = Blend(accent, panelBase, 0.9);
        var panelAlt = Blend(accent, panelAltBase, 0.93);
        var input = Blend(accent, inputBase, 0.95);
        var line = Blend(accent, Color.FromRgb(47, 70, 88), 0.58);

        var accentText = UseDarkText(accent) ? Color.FromRgb(17, 19, 26) : Colors.White;

        SetBrush("AccentBrush", accent);
        SetBrush("AccentTextBrush", accentText);
        SetBrush("AccentSoftBrush", softAccent);
        SetBrush("PanelBrush", panel);
        SetBrush("PanelAltBrush", panelAlt);
        SetBrush("InputBrush", input);
        SetBrush("InputHoverBrush", Blend(accent, input, 0.82));
        SetBrush("LineBrush", line);
        SetBrush("LineStrongBrush", Blend(accent, Color.FromRgb(90, 84, 101), 0.58));
        SetBrush("AppBackground", Blend(accent, shellBackground, 0.92));
        SetBrush(SystemColors.HighlightBrushKey, softAccent);
        SetBrush(SystemColors.InactiveSelectionHighlightBrushKey, softAccent);
        SetBrush(SystemColors.HighlightTextBrushKey, accentText);
        SetBrush(SystemColors.InactiveSelectionHighlightTextBrushKey, Colors.White);
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

    private void SetBrush(object key, Color color)
    {
        if (Resources[key] is SolidColorBrush brush && !brush.IsFrozen)
        {
            brush.Color = color;
            return;
        }

        Resources[key] = new SolidColorBrush(color);
    }

    private static Color Blend(Color source, Color target, double targetWeight)
    {
        var sourceWeight = 1 - targetWeight;
        return Color.FromRgb(
            (byte)((source.R * sourceWeight) + (target.R * targetWeight)),
            (byte)((source.G * sourceWeight) + (target.G * targetWeight)),
            (byte)((source.B * sourceWeight) + (target.B * targetWeight)));
    }

    private static bool UseDarkText(Color color)
    {
        var luminance = (0.2126 * color.R) + (0.7152 * color.G) + (0.0722 * color.B);
        return luminance > 150;
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

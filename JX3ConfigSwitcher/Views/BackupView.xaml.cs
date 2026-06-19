using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using JX3ConfigSwitcher.Models;
using JX3ConfigSwitcher.ViewModels;

namespace JX3ConfigSwitcher.Views;

public partial class BackupView : UserControl
{
    private Point _dragStartPoint;
    private Popup? _dragPopup;

    public BackupView()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyCurrentShellAccent();
    }

    public void ApplyAccent(Color accent)
    {
        var shellBackground = Color.FromRgb(48, 48, 48);
        var panelBase = Color.FromRgb(58, 58, 58);
        var panelAltBase = Color.FromRgb(63, 63, 63);
        var inputBase = Color.FromRgb(38, 38, 38);
        var softAccent = Blend(accent, Color.FromRgb(96, 96, 96), 0.86);
        var panel = Blend(accent, panelBase, 0.96);
        var panelAlt = Blend(accent, panelAltBase, 0.97);
        var input = Blend(accent, inputBase, 0.98);
        var line = Blend(accent, Color.FromRgb(89, 89, 89), 0.74);

        var accentText = UseDarkText(accent) ? Color.FromRgb(17, 19, 26) : Colors.White;

        SetBrush("AccentBrush", accent);
        SetBrush("AccentTextBrush", accentText);
        SetBrush("AccentSoftBrush", WithAlpha(softAccent, 210));
        SetBrush("PanelBrush", WithAlpha(panel, 204));
        SetBrush("PanelAltBrush", WithAlpha(panelAlt, 184));
        SetBrush("InputBrush", WithAlpha(input, 204));
        SetBrush("InputHoverBrush", WithAlpha(Blend(accent, input, 0.82), 214));
        SetBrush("LineBrush", line);
        SetBrush("LineStrongBrush", Blend(accent, Color.FromRgb(116, 116, 116), 0.68));
        SetBrush("AppBackground", shellBackground);
        SetBrush(SystemColors.HighlightBrushKey, softAccent);
        SetBrush(SystemColors.InactiveSelectionHighlightBrushKey, softAccent);
        SetBrush(SystemColors.HighlightTextBrushKey, accentText);
        SetBrush(SystemColors.InactiveSelectionHighlightTextBrushKey, Colors.White);
    }

    private void ApplyCurrentShellAccent()
    {
        if (Application.Current.TryFindResource("PrimaryHueMidBrush") is SolidColorBrush brush)
        {
            ApplyAccent(brush.Color);
        }
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
            element.GiveFeedback += SlotCard_GiveFeedback;
            StartDragPreview(slot);
            try
            {
                DragDrop.DoDragDrop(element, slot.Number, DragDropEffects.Copy);
            }
            finally
            {
                StopDragPreview();
                element.GiveFeedback -= SlotCard_GiveFeedback;
                AnimateDragSource(element, isDragging: false);
            }
        }
    }

    private void SlotCard_GiveFeedback(object sender, GiveFeedbackEventArgs e)
    {
        UpdateDragPreviewPosition();
        e.UseDefaultCursors = true;
        e.Handled = true;
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

    private void StartDragPreview(SlotViewModel slot)
    {
        StopDragPreview();
        var accentBrush = (Brush)FindResource("AccentBrush");
        var panelBrush = (Brush)FindResource("InputBrush");
        var lineBrush = (Brush)FindResource("LineStrongBrush");
        var mutedBrush = (Brush)FindResource("MutedBrush");
        var textBrush = (Brush)FindResource("TextBrush");

        _dragPopup = new Popup
        {
            AllowsTransparency = true,
            Placement = PlacementMode.RelativePoint,
            PlacementTarget = this,
            IsHitTestVisible = false,
            PopupAnimation = PopupAnimation.Fade,
            Child = new Border
            {
                Width = 168,
                Height = 84,
                Background = panelBrush,
                BorderBrush = accentBrush,
                BorderThickness = new Thickness(1.5),
                Padding = new Thickness(12),
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 22,
                    ShadowDepth = 6,
                    Opacity = 0.42
                },
                Child = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = new GridLength(4) },
                        new ColumnDefinition { Width = new GridLength(10) },
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                    },
                    Children =
                    {
                        CreateSectBar(slot.SectColor),
                        CreatePreviewText(slot, textBrush, mutedBrush, accentBrush)
                    }
                }
            }
        };

        UpdateDragPreviewPosition();
        _dragPopup.IsOpen = true;
    }

    private static Border CreateSectBar(string sectColor)
    {
        var brush = TryBrush(sectColor) ?? Brushes.DeepSkyBlue;
        return new Border
        {
            Background = brush
        };
    }

    private static StackPanel CreatePreviewText(SlotViewModel slot, Brush textBrush, Brush mutedBrush, Brush accentBrush)
    {
        var panel = new StackPanel { Margin = new Thickness(14, 0, 0, 0) };
        Grid.SetColumn(panel, 2);
        panel.Children.Add(new TextBlock
        {
            Text = slot.NumberText,
            Foreground = accentBrush,
            FontWeight = FontWeights.Bold,
            FontSize = 14
        });
        panel.Children.Add(new TextBlock
        {
            Text = slot.Name,
            Foreground = textBrush,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        panel.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(slot.SectTag) ? slot.KindText : slot.SectTag,
            Foreground = mutedBrush,
            FontSize = 11,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        return panel;
    }

    private void UpdateDragPreviewPosition()
    {
        if (_dragPopup is null)
        {
            return;
        }

        var point = Mouse.GetPosition(this);
        _dragPopup.HorizontalOffset = point.X + 8;
        _dragPopup.VerticalOffset = point.Y + 8;
    }

    private void StopDragPreview()
    {
        if (_dragPopup is null)
        {
            return;
        }

        _dragPopup.IsOpen = false;
        _dragPopup.Child = null;
        _dragPopup = null;
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

    private static Color WithAlpha(Color color, byte alpha) =>
        Color.FromArgb(alpha, color.R, color.G, color.B);

    private static Brush? TryBrush(string value)
    {
        try
        {
            return (Brush)new BrushConverter().ConvertFromString(value)!;
        }
        catch
        {
            return null;
        }
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

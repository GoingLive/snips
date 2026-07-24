using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Snips.App;

/// <summary>A semi-transparent ghost of the row being dragged, following the mouse — Roland's
/// direct ask ("there is often an image of the dragged row with the mouse"). Adorns the
/// ListBox itself (not the individual row) so its coordinate space matches
/// e.GetPosition(ResultsList) throughout the drag, regardless of which row started it.</summary>
internal sealed class DragAdorner : Adorner
{
    private readonly VisualBrush _brush;
    private readonly Size _size;
    private Point _mousePosition;

    public DragAdorner(UIElement adornedElement, UIElement draggedVisual) : base(adornedElement)
    {
        IsHitTestVisible = false;
        _brush = new VisualBrush(draggedVisual) { Opacity = 0.7 };
        _size = draggedVisual is FrameworkElement fe
            ? new Size(fe.ActualWidth, fe.ActualHeight)
            : new Size(200, 32);
    }

    public void UpdatePosition(Point mousePositionRelativeToAdornedElement)
    {
        _mousePosition = mousePositionRelativeToAdornedElement;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        // Offset so the ghost trails just below-right of the cursor rather than being centered
        // on it, matching the usual "drag handle" feel instead of obscuring the drop target.
        var rect = new Rect(_mousePosition.X + 12, _mousePosition.Y - _size.Height / 2, _size.Width, _size.Height);
        drawingContext.DrawRectangle(_brush, null, rect);
    }
}

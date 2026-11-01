using System;
using System.Windows;
using System.Windows.Controls;

namespace AppBar.Infrastructure
{
    public class CenteredUniformGrid : Panel
    {
        public static readonly DependencyProperty ColumnsProperty = DependencyProperty.Register(
            "Columns",
            typeof(int),
            typeof(CenteredUniformGrid),
            new FrameworkPropertyMetadata(5, FrameworkPropertyMetadataOptions.AffectsMeasure),
            ValidateColumns);

        public static readonly DependencyProperty HorizontalSpacingProperty =
            DependencyProperty.Register(
                "HorizontalSpacing",
                typeof(double),
                typeof(CenteredUniformGrid),
                new FrameworkPropertyMetadata(16.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty VerticalSpacingProperty =
            DependencyProperty.Register(
                "VerticalSpacing",
                typeof(double),
                typeof(CenteredUniformGrid),
                new FrameworkPropertyMetadata(18.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public int Columns
        {
            get { return (int)GetValue(ColumnsProperty); }
            set { SetValue(ColumnsProperty, value); }
        }

        public double HorizontalSpacing
        {
            get { return (double)GetValue(HorizontalSpacingProperty); }
            set { SetValue(HorizontalSpacingProperty, value); }
        }

        public double VerticalSpacing
        {
            get { return (double)GetValue(VerticalSpacingProperty); }
            set { SetValue(VerticalSpacingProperty, value); }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            int columns = Math.Max(1, Columns);
            int rows = (int)Math.Ceiling(InternalChildren.Count / (double)columns);
            double width = ResolveWidth(availableSize.Width, columns);
            double cellWidth = CalculateCellWidth(width, columns);
            double rowHeight = 0;

            foreach (UIElement child in InternalChildren)
            {
                child.Measure(new Size(cellWidth, double.PositiveInfinity));
                rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
            }

            double height = rows == 0
                ? 0
                : rows * rowHeight + Math.Max(0, rows - 1) * Math.Max(0, VerticalSpacing);
            return new Size(width, height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            int columns = Math.Max(1, Columns);
            double cellWidth = CalculateCellWidth(finalSize.Width, columns);
            double rowHeight = 0;

            foreach (UIElement child in InternalChildren)
            {
                rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
            }

            for (int index = 0; index < InternalChildren.Count; index++)
            {
                int row = index / columns;
                int column = index % columns;
                int firstIndex = row * columns;
                int itemsInRow = Math.Min(columns, InternalChildren.Count - firstIndex);
                double rowWidth = itemsInRow * cellWidth +
                                  Math.Max(0, itemsInRow - 1) * Math.Max(0, HorizontalSpacing);
                double startX = Math.Max(0, (finalSize.Width - rowWidth) / 2.0);
                double x = startX + column * (cellWidth + Math.Max(0, HorizontalSpacing));
                double y = row * (rowHeight + Math.Max(0, VerticalSpacing));
                InternalChildren[index].Arrange(new Rect(x, y, cellWidth, rowHeight));
            }

            return finalSize;
        }

        private double ResolveWidth(double availableWidth, int columns)
        {
            if (!double.IsInfinity(availableWidth) && availableWidth > 0)
            {
                return availableWidth;
            }

            double widestChild = 0;
            foreach (UIElement child in InternalChildren)
            {
                child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                widestChild = Math.Max(widestChild, child.DesiredSize.Width);
            }

            return widestChild * columns + Math.Max(0, columns - 1) * Math.Max(0, HorizontalSpacing);
        }

        private double CalculateCellWidth(double totalWidth, int columns)
        {
            double spacing = Math.Max(0, HorizontalSpacing);
            return Math.Max(1, (totalWidth - (columns - 1) * spacing) / columns);
        }

        private static bool ValidateColumns(object value)
        {
            return value is int && (int)value > 0;
        }
    }
}

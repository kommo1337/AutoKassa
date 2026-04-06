using System;
using System.Windows;
using System.Windows.Media;

namespace AutoKassa.Helpers
{
    public class DonutSliceItem
    {
        public string CategoryName { get; set; } = "";
        public string Color { get; set; } = "#cccccc";
        public double Percentage { get; set; }
        public decimal Amount { get; set; }
        public int TransactionCount { get; set; }
        public Geometry Geometry { get; set; } = Geometry.Empty;

        public static DonutSliceItem Create(
            string categoryName, string color,
            double percentage, decimal amount, int txCount,
            double startAngle, double sweepAngle,
            double cx = 130, double cy = 130,
            double outerR = 108, double innerR = 58)
        {
            return new DonutSliceItem
            {
                CategoryName = categoryName,
                Color = color,
                Percentage = percentage,
                Amount = amount,
                TransactionCount = txCount,
                Geometry = BuildGeometry(startAngle, sweepAngle, cx, cy, outerR, innerR)
            };
        }

        private static Geometry BuildGeometry(
            double startDeg, double sweepDeg,
            double cx, double cy, double outerR, double innerR)
        {
            if (sweepDeg >= 360) sweepDeg = 359.99;

            static double ToRad(double d) => d * Math.PI / 180.0;

            double startRad = ToRad(startDeg - 90);
            double endRad   = ToRad(startDeg + sweepDeg - 90);

            var p1 = new Point(cx + outerR * Math.Cos(startRad), cy + outerR * Math.Sin(startRad));
            var p2 = new Point(cx + outerR * Math.Cos(endRad),   cy + outerR * Math.Sin(endRad));
            var p3 = new Point(cx + innerR * Math.Cos(endRad),   cy + innerR * Math.Sin(endRad));
            var p4 = new Point(cx + innerR * Math.Cos(startRad), cy + innerR * Math.Sin(startRad));

            bool isLarge = sweepDeg > 180;

            var figure = new PathFigure { StartPoint = p1, IsClosed = true };
            figure.Segments.Add(new ArcSegment(p2, new Size(outerR, outerR), 0, isLarge, SweepDirection.Clockwise,        true));
            figure.Segments.Add(new LineSegment(p3, true));
            figure.Segments.Add(new ArcSegment(p4, new Size(innerR, innerR), 0, isLarge, SweepDirection.Counterclockwise, true));

            return new PathGeometry(new[] { figure });
        }
    }
}

using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using FlowWatch.Helpers;

namespace FlowWatch.Controls
{
    public class MathCurveLoaderElement : FrameworkElement
    {
        private const int PathSteps = 480;
        private const double MinMotionMultiplier = 0.35;
        private const double MaxMotionMultiplier = 3.0;
        private const double MotionSmoothingMs = 180.0;
        private const double StableDetailScale = 0.76;
        private const double TransitionDurationMs = 450.0;
        private const double VirtualCanvasCenter = 50.0;
        private const double TargetCurveSize = 78.0;
        private const double MinNormalizableCurveSize = 0.001;

        public static readonly DependencyProperty CurveKeyProperty =
            DependencyProperty.Register(
                nameof(CurveKey),
                typeof(string),
                typeof(MathCurveLoaderElement),
                new FrameworkPropertyMetadata(
                    MathCurveCatalog.DefaultKey,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    OnCurveKeyChanged));

        public static readonly DependencyProperty MotionRatioProperty =
            DependencyProperty.Register(
                nameof(MotionRatio),
                typeof(double),
                typeof(MathCurveLoaderElement),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SpiralBrushProperty =
            DependencyProperty.Register(
                nameof(SpiralBrush),
                typeof(Brush),
                typeof(MathCurveLoaderElement),
                new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

        private bool _renderingSubscribed;
        private long _lastFrameTick;
        private double _motionTimeMs;
        private double _currentMotionMultiplier = MinMotionMultiplier;
        private string _currentCurveKey = MathCurveCatalog.DefaultKey;
        private string _previousCurveKey;
        private long _transitionStartTick;
        private bool _isTransitioning;

        public MathCurveLoaderElement()
        {
            SnapsToDevicePixels = false;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            IsVisibleChanged += OnIsVisibleChanged;
        }

        public string CurveKey
        {
            get => (string)GetValue(CurveKeyProperty);
            set => SetValue(CurveKeyProperty, value);
        }

        public double MotionRatio
        {
            get => (double)GetValue(MotionRatioProperty);
            set => SetValue(MotionRatioProperty, value);
        }

        public Brush SpiralBrush
        {
            get => (Brush)GetValue(SpiralBrushProperty);
            set => SetValue(SpiralBrushProperty, value);
        }

        private static void OnCurveKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var element = d as MathCurveLoaderElement;
            element?.StartCurveTransition(e.NewValue as string);
        }

        private void StartCurveTransition(string curveKey)
        {
            string nextKey = MathCurveCatalog.Get(curveKey).Key;
            if (string.Equals(nextKey, _currentCurveKey, StringComparison.OrdinalIgnoreCase))
                return;

            _previousCurveKey = _currentCurveKey;
            _currentCurveKey = nextKey;
            _transitionStartTick = Stopwatch.GetTimestamp();
            _isTransitioning = true;
            UpdateRenderingSubscription();
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            double width = ActualWidth;
            double height = ActualHeight;
            if (width <= 0.0 || height <= 0.0)
                return;

            double size = Math.Min(width, height);
            double scale = size / 100.0;
            var offset = new Vector((width - size) / 2.0, (height - size) / 2.0);
            var brush = SpiralBrush ?? Brushes.White;
            double transitionProgress = GetTransitionProgress();
            var currentDefinition = MathCurveCatalog.Get(_currentCurveKey);

            if (_isTransitioning && !string.IsNullOrEmpty(_previousCurveKey))
            {
                var previousDefinition = MathCurveCatalog.Get(_previousCurveKey);
                RenderCurve(drawingContext, previousDefinition, brush, scale, offset, 1.0 - transitionProgress);
                RenderCurve(drawingContext, currentDefinition, brush, scale, offset, transitionProgress);
            }
            else
            {
                RenderCurve(drawingContext, currentDefinition, brush, scale, offset, 1.0);
            }
        }

        private void RenderCurve(
            DrawingContext drawingContext,
            MathCurveDefinition definition,
            Brush brush,
            double scale,
            Vector offset,
            double opacity)
        {
            if (opacity <= 0.0)
                return;

            double progress = NormalizeProgress((_motionTimeMs % definition.DurationMs) / definition.DurationMs);
            var samples = BuildCurveSamples(definition, StableDetailScale, scale, offset);

            var pen = new Pen(brush, definition.StrokeWidth * scale)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };

            drawingContext.PushOpacity(opacity * 0.12);
            drawingContext.DrawGeometry(null, pen, BuildPathGeometry(samples));
            drawingContext.Pop();

            for (int index = definition.ParticleCount - 1; index >= 0; index--)
            {
                double tailOffset = index / (double)(definition.ParticleCount - 1);
                double particleProgress = NormalizeProgress(progress - tailOffset * definition.TrailSpan);
                double fade = Math.Pow(1.0 - tailOffset, 0.56);
                var point = GetPointAtArcProgress(samples, particleProgress);
                double radius = (0.9 + fade * 2.7) * scale;
                double particleOpacity = opacity * (0.04 + fade * 0.96);

                drawingContext.PushOpacity(particleOpacity);
                drawingContext.DrawEllipse(brush, null, point, radius, radius);
                drawingContext.Pop();
            }
        }

        private double GetTransitionProgress()
        {
            if (!_isTransitioning)
                return 1.0;

            double elapsedMs = (Stopwatch.GetTimestamp() - _transitionStartTick) * 1000.0 / Stopwatch.Frequency;
            double progress = Math.Max(0.0, Math.Min(1.0, elapsedMs / TransitionDurationMs));
            if (progress >= 1.0)
            {
                _isTransitioning = false;
                _previousCurveKey = null;
            }

            return EaseInOut(progress);
        }

        private static double EaseInOut(double progress)
        {
            return progress < 0.5
                ? 2.0 * progress * progress
                : 1.0 - Math.Pow(-2.0 * progress + 2.0, 2.0) / 2.0;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateRenderingSubscription();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            StopRendering();
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UpdateRenderingSubscription();
        }

        private void UpdateRenderingSubscription()
        {
            if (IsLoaded && IsVisible)
                StartRendering();
            else
                StopRendering();
        }

        private void StartRendering()
        {
            if (_renderingSubscribed)
                return;

            _lastFrameTick = Stopwatch.GetTimestamp();
            CompositionTarget.Rendering += OnRendering;
            _renderingSubscribed = true;
            InvalidateVisual();
        }

        private void StopRendering()
        {
            if (!_renderingSubscribed)
                return;

            CompositionTarget.Rendering -= OnRendering;
            _renderingSubscribed = false;
            _lastFrameTick = 0;
        }

        private void OnRendering(object sender, EventArgs e)
        {
            long now = Stopwatch.GetTimestamp();
            if (_lastFrameTick == 0)
            {
                _lastFrameTick = now;
                InvalidateVisual();
                return;
            }

            double elapsedMs = (now - _lastFrameTick) * 1000.0 / Stopwatch.Frequency;
            _lastFrameTick = now;
            elapsedMs = Math.Max(0.0, Math.Min(80.0, elapsedMs));
            _currentMotionMultiplier += (GetTargetMotionMultiplier() - _currentMotionMultiplier) * GetSmoothingFactor(elapsedMs);
            _motionTimeMs += elapsedMs * _currentMotionMultiplier;
            InvalidateVisual();
        }

        private double GetTargetMotionMultiplier()
        {
            double ratio = Math.Min(1.0, Math.Max(0.0, MotionRatio));
            return MinMotionMultiplier + (MaxMotionMultiplier - MinMotionMultiplier) * ratio;
        }

        private static double GetSmoothingFactor(double elapsedMs)
        {
            return 1.0 - Math.Exp(-elapsedMs / MotionSmoothingMs);
        }

        private static CurveSamples BuildCurveSamples(MathCurveDefinition definition, double detailScale, double scale, Vector offset)
        {
            var rawPoints = new Point[PathSteps + 1];
            var points = new Point[PathSteps + 1];
            var cumulativeLengths = new double[PathSteps + 1];

            double minX = double.MaxValue;
            double maxX = double.MinValue;
            double minY = double.MaxValue;
            double maxY = double.MinValue;

            for (int index = 0; index <= PathSteps; index++)
            {
                var rawPoint = definition.Point(index / (double)PathSteps, detailScale);
                rawPoints[index] = rawPoint;
                minX = Math.Min(minX, rawPoint.X);
                maxX = Math.Max(maxX, rawPoint.X);
                minY = Math.Min(minY, rawPoint.Y);
                maxY = Math.Max(maxY, rawPoint.Y);
            }

            double curveWidth = maxX - minX;
            double curveHeight = maxY - minY;
            double maxCurveSize = Math.Max(curveWidth, curveHeight);
            double curveScale = maxCurveSize > MinNormalizableCurveSize
                ? TargetCurveSize / maxCurveSize
                : 1.0;
            double centerX = (minX + maxX) / 2.0;
            double centerY = (minY + maxY) / 2.0;

            points[0] = MapPoint(NormalizeCurvePoint(rawPoints[0], centerX, centerY, curveScale), scale, offset);

            double totalLength = 0.0;
            for (int index = 1; index <= PathSteps; index++)
            {
                points[index] = MapPoint(NormalizeCurvePoint(rawPoints[index], centerX, centerY, curveScale), scale, offset);
                totalLength += Distance(points[index - 1], points[index]);
                cumulativeLengths[index] = totalLength;
            }

            return new CurveSamples(points, cumulativeLengths, totalLength);
        }

        private static Point NormalizeCurvePoint(Point point, double centerX, double centerY, double curveScale)
        {
            return new Point(
                VirtualCanvasCenter + (point.X - centerX) * curveScale,
                VirtualCanvasCenter + (point.Y - centerY) * curveScale);
        }

        private static PathGeometry BuildPathGeometry(CurveSamples samples)
        {
            var figure = new PathFigure
            {
                StartPoint = samples.Points[0],
                IsClosed = false,
                IsFilled = false
            };

            var segment = new PolyLineSegment();
            for (int index = 1; index <= PathSteps; index++)
            {
                segment.Points.Add(samples.Points[index]);
            }

            figure.Segments.Add(segment);
            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            return geometry;
        }

        private static Point GetPointAtArcProgress(CurveSamples samples, double progress)
        {
            if (samples.TotalLength <= 0.0)
                return samples.Points[0];

            double targetLength = progress * samples.TotalLength;
            int low = 0;
            int high = samples.CumulativeLengths.Length - 1;

            while (low < high)
            {
                int mid = low + (high - low) / 2;
                if (samples.CumulativeLengths[mid] < targetLength)
                    low = mid + 1;
                else
                    high = mid;
            }

            int index = Math.Max(1, low);
            double previousLength = samples.CumulativeLengths[index - 1];
            double segmentLength = samples.CumulativeLengths[index] - previousLength;
            double segmentProgress = segmentLength <= 0.0
                ? 0.0
                : (targetLength - previousLength) / segmentLength;

            return Lerp(samples.Points[index - 1], samples.Points[index], segmentProgress);
        }

        private static Point MapPoint(Point point, double scale, Vector offset)
        {
            return new Point(offset.X + point.X * scale, offset.Y + point.Y * scale);
        }

        private static Point Lerp(Point first, Point second, double progress)
        {
            return new Point(
                first.X + (second.X - first.X) * progress,
                first.Y + (second.Y - first.Y) * progress);
        }

        private static double Distance(Point first, Point second)
        {
            double dx = second.X - first.X;
            double dy = second.Y - first.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static double NormalizeProgress(double value)
        {
            value %= 1.0;
            return value < 0.0 ? value + 1.0 : value;
        }

        private sealed class CurveSamples
        {
            public CurveSamples(Point[] points, double[] cumulativeLengths, double totalLength)
            {
                Points = points;
                CumulativeLengths = cumulativeLengths;
                TotalLength = totalLength;
            }

            public Point[] Points { get; }
            public double[] CumulativeLengths { get; }
            public double TotalLength { get; }
        }
    }
}

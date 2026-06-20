using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace FlowWatch.Helpers
{
    public sealed class MathCurveAnimationOption
    {
        public MathCurveAnimationOption(string key, string displayName)
        {
            Key = key;
            DisplayName = displayName;
        }

        public string Key { get; }
        public string DisplayName { get; }

        public override string ToString()
        {
            return DisplayName;
        }
    }

    public sealed class MathCurveDefinition
    {
        public MathCurveDefinition(
            string key,
            string displayName,
            int particleCount,
            double trailSpan,
            double durationMs,
            double strokeWidth,
            Func<double, double, Point> point)
        {
            Key = key;
            DisplayName = displayName;
            ParticleCount = particleCount;
            TrailSpan = trailSpan;
            DurationMs = durationMs;
            StrokeWidth = strokeWidth;
            Point = point;
        }

        public string Key { get; }
        public string DisplayName { get; }
        public int ParticleCount { get; }
        public double TrailSpan { get; }
        public double DurationMs { get; }
        public double StrokeWidth { get; }
        public Func<double, double, Point> Point { get; }
    }

    public static class MathCurveCatalog
    {
        public const string DefaultKey = "three-petal-spiral";
        public const string RandomKey = "random";

        private static readonly ReadOnlyCollection<MathCurveDefinition> _all;
        private static readonly ReadOnlyCollection<MathCurveAnimationOption> _options;
        private static readonly Dictionary<string, MathCurveDefinition> _byKey;

        static MathCurveCatalog()
        {
            var definitions = new List<MathCurveDefinition>
            {
                Thinking("original-thinking", "Original Thinking", 7, 64, 0.38, 4600, 5.5),
                Thinking("thinking-five", "Thinking Five", 5, 62, 0.38, 4600, 5.5),
                Thinking("thinking-nine", "Thinking Nine", 9, 68, 0.39, 4700, 5.5),
                RoseOrbit(),
                Rose("rose-curve", "Rose Curve", 5, 78, 0.32, 5400, 4.5),
                Rose("rose-two", "Rose Two", 2, 74, 0.30, 5200, 4.6),
                Rose("rose-three", "Rose Three", 3, 76, 0.31, 5300, 4.6),
                Rose("rose-four", "Rose Four", 4, 78, 0.32, 5400, 4.6),
                LissajousDrift(),
                LemniscateBloom(),
                HypotrochoidLoop(),
                Spiral("three-petal-spiral", "Three-Petal Spiral", 3, 82),
                Spiral("four-petal-spiral", "Four-Petal Spiral", 4, 84),
                Spiral("five-petal-spiral", "Five-Petal Spiral", 5, 85),
                Spiral("six-petal-spiral", "Six-Petal Spiral", 6, 86),
                ButterflyPhase(),
                CardioidGlow(),
                CardioidHeart(),
                HeartWave(),
                SpiralSearch(),
                FourierFlow()
            };

            _all = definitions.AsReadOnly();
            _options = definitions
                .Select(d => new MathCurveAnimationOption(d.Key, d.DisplayName))
                .ToList()
                .AsReadOnly();
            _byKey = definitions.ToDictionary(d => d.Key, StringComparer.OrdinalIgnoreCase);
        }

        public static IReadOnlyList<MathCurveDefinition> All => _all;
        public static IReadOnlyList<MathCurveAnimationOption> Options => _options;

        public static IReadOnlyList<MathCurveAnimationOption> CreateOptions(string randomDisplayName)
        {
            var options = new List<MathCurveAnimationOption>
            {
                new MathCurveAnimationOption(RandomKey, randomDisplayName)
            };
            options.AddRange(_options);
            return options.AsReadOnly();
        }

        public static bool IsRandomKey(string key)
        {
            return string.Equals(key, RandomKey, StringComparison.OrdinalIgnoreCase);
        }

        public static string NormalizeKey(string key)
        {
            MathCurveDefinition definition;
            if (IsRandomKey(key))
                return RandomKey;

            return !string.IsNullOrWhiteSpace(key) && _byKey.TryGetValue(key, out definition)
                ? definition.Key
                : DefaultKey;
        }

        public static MathCurveDefinition Get(string key)
        {
            MathCurveDefinition definition;
            return !string.IsNullOrWhiteSpace(key) && _byKey.TryGetValue(key, out definition)
                ? definition
                : _byKey[DefaultKey];
        }

        private static MathCurveDefinition Thinking(
            string key,
            string displayName,
            int petals,
            int particleCount,
            double trailSpan,
            double durationMs,
            double strokeWidth)
        {
            return new MathCurveDefinition(
                key,
                displayName,
                particleCount,
                trailSpan,
                durationMs,
                strokeWidth,
                (progress, detailScale) =>
                {
                    double t = progress * Math.PI * 2.0;
                    double x = 7.0 * Math.Cos(t) - 3.0 * detailScale * Math.Cos(petals * t);
                    double y = 7.0 * Math.Sin(t) - 3.0 * detailScale * Math.Sin(petals * t);
                    return new Point(50.0 + x * 3.9, 50.0 + y * 3.9);
                });
        }

        private static MathCurveDefinition RoseOrbit()
        {
            return new MathCurveDefinition(
                "rose-orbit",
                "Rose Orbit",
                72,
                0.42,
                5200,
                5.2,
                (progress, detailScale) =>
                {
                    double t = progress * Math.PI * 2.0;
                    double r = 7.0 - 2.7 * detailScale * Math.Cos(7.0 * t);
                    return new Point(
                        50.0 + Math.Cos(t) * r * 3.9,
                        50.0 + Math.Sin(t) * r * 3.9);
                });
        }

        private static MathCurveDefinition Rose(
            string key,
            string displayName,
            int k,
            int particleCount,
            double trailSpan,
            double durationMs,
            double strokeWidth)
        {
            return new MathCurveDefinition(
                key,
                displayName,
                particleCount,
                trailSpan,
                durationMs,
                strokeWidth,
                (progress, detailScale) =>
                {
                    double t = progress * Math.PI * 2.0;
                    double a = 9.2 + detailScale * 0.6;
                    double r = a * (0.72 + detailScale * 0.28) * Math.Cos(k * t);
                    return new Point(
                        50.0 + Math.Cos(t) * r * 3.25,
                        50.0 + Math.Sin(t) * r * 3.25);
                });
        }

        private static MathCurveDefinition LissajousDrift()
        {
            return new MathCurveDefinition(
                "lissajous-drift",
                "Lissajous Drift",
                68,
                0.34,
                6000,
                4.7,
                (progress, detailScale) =>
                {
                    double t = progress * Math.PI * 2.0;
                    double amp = 24.0 + detailScale * 6.0;
                    return new Point(
                        50.0 + Math.Sin(3.0 * t + 1.57) * amp,
                        50.0 + Math.Sin(4.0 * t) * amp * 0.92);
                });
        }

        private static MathCurveDefinition LemniscateBloom()
        {
            return new MathCurveDefinition(
                "lemniscate-bloom",
                "Lemniscate Bloom",
                70,
                0.40,
                5600,
                4.8,
                (progress, detailScale) =>
                {
                    double t = progress * Math.PI * 2.0;
                    double scale = 20.0 + detailScale * 7.0;
                    double denom = 1.0 + Math.Pow(Math.Sin(t), 2.0);
                    return new Point(
                        50.0 + scale * Math.Cos(t) / denom,
                        50.0 + scale * Math.Sin(t) * Math.Cos(t) / denom);
                });
        }

        private static MathCurveDefinition HypotrochoidLoop()
        {
            return new MathCurveDefinition(
                "hypotrochoid-loop",
                "Hypotrochoid Loop",
                82,
                0.46,
                7600,
                4.6,
                (progress, detailScale) =>
                {
                    double t = progress * Math.PI * 2.0;
                    double r = 2.7 + detailScale * 0.45;
                    double d = 4.8 + detailScale * 1.2;
                    double x = (8.2 - r) * Math.Cos(t) + d * Math.Cos(((8.2 - r) / r) * t);
                    double y = (8.2 - r) * Math.Sin(t) - d * Math.Sin(((8.2 - r) / r) * t);
                    return new Point(50.0 + x * 3.05, 50.0 + y * 3.05);
                });
        }

        private static MathCurveDefinition Spiral(string key, string displayName, double spiralR, int particleCount)
        {
            return new MathCurveDefinition(
                key,
                displayName,
                particleCount,
                0.34,
                4600,
                4.4,
                (progress, detailScale) =>
                {
                    double t = progress * Math.PI * 2.0;
                    double d = 3.0 + detailScale * 0.25;
                    double baseX = (spiralR - 1.0) * Math.Cos(t) + d * Math.Cos((spiralR - 1.0) * t);
                    double baseY = (spiralR - 1.0) * Math.Sin(t) - d * Math.Sin((spiralR - 1.0) * t);
                    double scale = 2.2 + detailScale * 0.45;
                    return new Point(50.0 + baseX * scale, 50.0 + baseY * scale);
                });
        }

        private static MathCurveDefinition ButterflyPhase()
        {
            return new MathCurveDefinition(
                "butterfly-phase",
                "Butterfly Phase",
                88,
                0.32,
                9000,
                4.4,
                (progress, detailScale) =>
                {
                    double t = progress * Math.PI * 12.0;
                    double s = Math.Exp(Math.Cos(t)) -
                               2.0 * Math.Cos(4.0 * t) -
                               Math.Pow(Math.Sin(t / 12.0), 5.0);
                    double scale = 4.6 + detailScale * 0.45;
                    return new Point(
                        50.0 + Math.Sin(t) * s * scale,
                        50.0 + Math.Cos(t) * s * scale);
                });
        }

        private static MathCurveDefinition CardioidGlow()
        {
            return new MathCurveDefinition(
                "cardioid-glow",
                "Cardioid Glow",
                72,
                0.36,
                6200,
                4.9,
                (progress, detailScale) =>
                {
                    double t = progress * Math.PI * 2.0;
                    double a = 8.4 + detailScale * 0.8;
                    double r = a * (1.0 - Math.Cos(t));
                    return new Point(
                        50.0 + Math.Cos(t) * r * 2.15,
                        50.0 + Math.Sin(t) * r * 2.15);
                });
        }

        private static MathCurveDefinition CardioidHeart()
        {
            return new MathCurveDefinition(
                "cardioid-heart",
                "Cardioid Heart",
                74,
                0.36,
                6200,
                4.9,
                (progress, detailScale) =>
                {
                    double t = progress * Math.PI * 2.0;
                    double a = 8.8 + detailScale * 0.8;
                    double r = a * (1.0 + Math.Cos(t));
                    double baseX = Math.Cos(t) * r;
                    double baseY = Math.Sin(t) * r;
                    return new Point(50.0 - baseY * 2.15, 50.0 - baseX * 2.15);
                });
        }

        private static MathCurveDefinition HeartWave()
        {
            return new MathCurveDefinition(
                "heart-wave",
                "Heart Wave",
                104,
                0.18,
                8400,
                3.9,
                (progress, detailScale) =>
                {
                    double xLimit = Math.Sqrt(3.3);
                    double x = -xLimit + progress * xLimit * 2.0;
                    double safeRoot = Math.Max(0.0, 3.3 - x * x);
                    double wave = 0.9 * Math.Sqrt(safeRoot) * Math.Sin(6.4 * Math.PI * x);
                    double curve = Math.Pow(Math.Abs(x), 2.0 / 3.0);
                    double y = curve + wave;
                    return new Point(
                        50.0 + x * 23.2,
                        18.0 + (1.75 - y) * (24.5 + detailScale * 1.5));
                });
        }

        private static MathCurveDefinition SpiralSearch()
        {
            return new MathCurveDefinition(
                "spiral-search",
                "Spiral Search",
                86,
                0.28,
                7800,
                4.3,
                (progress, detailScale) =>
                {
                    double t = progress * Math.PI * 2.0;
                    double angle = t * 4.0;
                    double radius = 8.0 + (1.0 - Math.Cos(t)) * (8.5 + detailScale * 2.4);
                    return new Point(
                        50.0 + Math.Cos(angle) * radius,
                        50.0 + Math.Sin(angle) * radius);
                });
        }

        private static MathCurveDefinition FourierFlow()
        {
            return new MathCurveDefinition(
                "fourier-flow",
                "Fourier Flow",
                92,
                0.31,
                8400,
                4.2,
                (progress, detailScale) =>
                {
                    double t = progress * Math.PI * 2.0;
                    double mix = 1.0 + detailScale * 0.16;
                    double x = 17.0 * Math.Cos(t) +
                               7.5 * Math.Cos(3.0 * t + 0.6 * mix) +
                               3.2 * Math.Sin(5.0 * t - 0.4);
                    double y = 15.0 * Math.Sin(t) +
                               8.2 * Math.Sin(2.0 * t + 0.25) -
                               4.2 * Math.Cos(4.0 * t - 0.5 * mix);
                    return new Point(50.0 + x, 50.0 + y);
                });
        }
    }
}

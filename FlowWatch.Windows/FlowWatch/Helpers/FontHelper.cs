using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using FlowWatch.Services;

namespace FlowWatch.Helpers
{
    public static class FontHelper
    {
        private static readonly object _lock = new object();
        private static readonly Dictionary<string, FontFamily> _cache =
            new Dictionary<string, FontFamily>(StringComparer.OrdinalIgnoreCase);

        private static readonly string[] FallbackFamilies =
        {
            "Segoe UI",
            "Microsoft YaHei",
            "Arial",
            "Consolas"
        };

        public static FontFamily ResolveFontFamily(string preferredList, string context)
        {
            var cacheKey = string.IsNullOrWhiteSpace(preferredList)
                ? string.Empty
                : preferredList.Trim();

            lock (_lock)
            {
                if (_cache.TryGetValue(cacheKey, out var cached))
                    return cached;
            }

            var candidates = EnumerateCandidates(preferredList).ToList();
            var requested = candidates.FirstOrDefault() ?? "Segoe UI";

            foreach (var candidate in candidates)
            {
                if (TryCreateUsableFontFamily(candidate, out var family))
                {
                    if (!string.Equals(candidate, requested, StringComparison.OrdinalIgnoreCase))
                    {
                        LogService.Warn(
                            $"Font fallback applied for {context}: requested '{requested}', using '{candidate}'.");
                    }

                    lock (_lock)
                    {
                        _cache[cacheKey] = family;
                    }

                    return family;
                }
            }

            var fallback = new FontFamily("Segoe UI");
            LogService.Warn(
                $"No usable font found for {context}: '{preferredList}'. Falling back to 'Segoe UI'.");

            lock (_lock)
            {
                _cache[cacheKey] = fallback;
            }

            return fallback;
        }

        private static IEnumerable<string> EnumerateCandidates(string preferredList)
        {
            if (!string.IsNullOrWhiteSpace(preferredList))
            {
                foreach (var candidate in preferredList
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(part => part.Trim())
                    .Where(part => !string.IsNullOrWhiteSpace(part))
                    .Where(part => !IsGenericFamily(part)))
                {
                    yield return candidate;
                }
            }

            foreach (var fallback in FallbackFamilies)
                yield return fallback;
        }

        private static bool IsGenericFamily(string name)
        {
            return string.Equals(name, "sans-serif", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "serif", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "monospace", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "cursive", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "fantasy", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "system-ui", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryCreateUsableFontFamily(string name, out FontFamily family)
        {
            family = null;

            try
            {
                var candidate = new FontFamily(name);
                var typeface = new Typeface(
                    candidate,
                    FontStyles.Normal,
                    FontWeights.Normal,
                    FontStretches.Normal);

                GlyphTypeface glyphTypeface;
                if (!typeface.TryGetGlyphTypeface(out glyphTypeface))
                    return false;

                family = candidate;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}

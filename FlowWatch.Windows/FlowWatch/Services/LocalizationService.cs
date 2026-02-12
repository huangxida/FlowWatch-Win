using System;
using System.Globalization;
using System.Windows;

namespace FlowWatch.Services
{
    public class LocalizationService
    {
        private static readonly Lazy<LocalizationService> _instance =
            new Lazy<LocalizationService>(() => new LocalizationService());
        public static LocalizationService Instance => _instance.Value;

        private ResourceDictionary _currentStringDict;

        public event Action LanguageChanged;

        public string ResolveLanguage(string setting)
        {
            if (setting == "zh") return "zh";
            if (setting == "en") return "en";
            // auto: detect from system culture
            var culture = CultureInfo.CurrentUICulture;
            return culture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? "zh" : "en";
        }

        public void ApplyLanguage(string setting)
        {
            var lang = ResolveLanguage(setting);
            var uri = lang == "zh"
                ? new Uri("Resources/Strings/zh-CN.xaml", UriKind.Relative)
                : new Uri("Resources/Strings/en-US.xaml", UriKind.Relative);

            var dict = new ResourceDictionary { Source = uri };
            var mergedDicts = Application.Current.Resources.MergedDictionaries;

            if (_currentStringDict != null)
                mergedDicts.Remove(_currentStringDict);

            mergedDicts.Add(dict);
            _currentStringDict = dict;

            LanguageChanged?.Invoke();
        }

        public string Get(string key)
        {
            return Application.Current.TryFindResource(key) as string ?? key;
        }

        public string Format(string key, params object[] args)
        {
            var template = Get(key);
            return string.Format(template, args);
        }
    }
}

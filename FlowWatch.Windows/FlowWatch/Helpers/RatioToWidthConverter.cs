using System;
using System.Globalization;
using System.Windows.Data;

namespace FlowWatch.Helpers
{
    public class RatioToWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 2
                && values[0] is double ratio
                && values[1] is double containerWidth
                && containerWidth > 0)
            {
                return Math.Max(0, ratio * containerWidth);
            }
            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}

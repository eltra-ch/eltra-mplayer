using System;
using System.Globalization;
using Xamarin.Forms;

namespace EltraNavigoMPlayer.Views.MediaControl.Converters
{
    class CompositionPositionToIndexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int result = -1;
            
            if(value is int position)
            {
                result = position + 1;
            }

            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

using System;
using System.Globalization;
using Xamarin.Forms;

namespace EltraNavigoMPlayer.Views.MPlayerControl.Converters
{
    class StopPlayStateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool result = false;

            if(value is int activeStationValue)
            {
                if(activeStationValue > 0)
                {
                    result = true;
                }
            }

            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

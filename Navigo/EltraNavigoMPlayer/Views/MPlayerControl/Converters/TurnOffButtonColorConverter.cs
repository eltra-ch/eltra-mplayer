﻿using System;
using System.Globalization;
using Xamarin.Forms;

namespace EltraNavigoMPlayer.Views.MPlayerControl.Converters
{
    class TurnOffButtonColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var result = Color.LightCoral;
            
            if (value is ushort relayState)
            {
                if(relayState == 0)
                {
                    result = Color.SlateGray;
                }
                else if(relayState == 1)
                {
                    result = Color.LightSeaGreen;
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

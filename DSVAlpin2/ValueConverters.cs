﻿using DSVAlpin2Lib;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace DSVAlpin2
{
  /// <summary>
  /// Converts a position number into a string. Position 0 is transferred into "./."
  /// </summary>
  public class PositionConverter : IValueConverter
  {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
      uint position = (uint)value;

      if (position == 0)
        return "./.";

      return position.ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
      throw new NotImplementedException();
    }
  }

  public class BooleanToBrushConverter : IValueConverter
  {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
      if (value == null)
        return Brushes.Transparent;

      Brush[] brushes = parameter as Brush[];
      if (brushes == null)
        return Brushes.Transparent;

      bool isTrue;
      bool.TryParse(value.ToString(), out isTrue);

      if (isTrue)
      {
        var brush = (SolidColorBrush)brushes[0];
        return brush ?? Brushes.Transparent;
      }
      else
      {
        var brush = (SolidColorBrush)brushes[1];
        return brush ?? Brushes.Transparent;
      }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
      throw new NotImplementedException();
    }
  }


  public class ResultCodeConverter : IValueConverter
  {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
      try
      {
        RunResult.EResultCode rc = (RunResult.EResultCode)value;

        if (rc == RunResult.EResultCode.Normal)
          return "";

        return rc.ToString();
      }
      catch (Exception)
      {
        return "";
      }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
      throw new NotImplementedException();
    }
  }


  public class ResultCodeWithCommentConverter : IMultiValueConverter
  {
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
      if (values.Length != 2)
        throw new Exception("invalid number of argumnets");

      try
      {
        RunResult.EResultCode rc = (RunResult.EResultCode)values[0];
        string comment = (string)values[1];

        if (rc == RunResult.EResultCode.Normal)
          return "";

        if (string.IsNullOrEmpty(comment))
          return rc.ToString();
        else
          return string.Format("{0} ({1})", rc.ToString(), comment);
      }
      catch(Exception)
      {
        return "";
      }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
      throw new NotImplementedException();
    }
  }

}

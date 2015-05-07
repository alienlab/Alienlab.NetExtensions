using System;

namespace Alienlab.NetExtensions
{
  internal class NotNullAttribute : Attribute
  {
  }

  internal static class Assert
  {
    internal static void ArgumentNotNull(object argument, string argumentName)
    {
      if (null == argument)
      {
        throw new ArgumentNullException(argumentName);
      }
    }

    internal static void ArgumentNotNullOrEmpty(string argument, string argumentName)
    {
      if (string.IsNullOrEmpty(argumentName))
      {
        throw new ArgumentException(string.Format("The {0} argument must not be null", argumentName));
      }

      if (string.IsNullOrEmpty(argument))
      {
        throw new ArgumentException(string.Format("The {0} argument must not be null", argumentName));
      }
    }

    internal static void IsNotNull(object value, string errorMessage)
    {
      if (null == value)
      {
        throw new InvalidOperationException("The object must not be null. " + errorMessage);
      }
    }

    internal static void IsNotNullOrEmpty(string value, string errorMessage)
    {
      if(string.IsNullOrEmpty(value))
      {
        throw new InvalidOperationException(errorMessage);
      }
    }
    
    internal static void IsTrue(bool condition)
    {
      if(!condition)
      {
        throw new InvalidOperationException("The condition is not valid.");
      }
    }
    internal static void IsTrue(bool condition, string errorMessage)
    {
      if (!condition)
      {
        throw new InvalidOperationException(errorMessage);
      }
    }
  }
}

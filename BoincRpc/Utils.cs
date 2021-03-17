using System;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace BoincRpc
{
    internal static class Utils
    {
        public static string GetMD5Hash(string s)
        {
            byte[] hash;

            using (MD5 md5 = MD5.Create())
                hash = md5.ComputeHash(Encoding.ASCII.GetBytes(s));

            StringBuilder hashString = new StringBuilder(hash.Length * 2);

            for (int i = 0; i < hash.Length; i++)
                hashString.Append(hash[i].ToString("x2"));

            return hashString.ToString();
        }

        public static DateTimeOffset ConvertUnixTimeToDateTime(double unixTime)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds((long)(unixTime * 1000));
        }

        public static double ConvertDateTimeToUnixTime(DateTimeOffset dateTime)
        {
            return dateTime.ToUnixTimeMilliseconds() / 1000;
        }

        public static TimeSpan ConvertSecondsToTimeSpan(double seconds)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        public static double ConvertTimeSpanToSeconds(TimeSpan timeSpan)
        {
            return timeSpan.TotalSeconds;
        }
    }

    internal static class ExtensionsMethods
    {
        public static bool ContainsElement(this XElement element, XName name)
        {
            return element.Element(name) != null;
        }

        public static bool ElementBoolean(this XElement element, XName name, bool defaultValue = default)
        {
            XElement child = element.Element(name);

            if (child == null)
                return defaultValue;
            else
                if (child.IsEmpty)
                    return true;
                else
                    return (bool)child;
        }

        public static int ElementInt(this XElement element, XName name, int defaultValue = default)
        {
            return ((int?)element.Element(name)).GetValueOrDefault(defaultValue);
        }

        public static double ElementDouble(this XElement element, XName name, double defaultValue = default)
        {
            return ((double?)element.Element(name)).GetValueOrDefault(defaultValue);
        }

        public static string ElementString(this XElement element, XName name, string defaultValue = default)
        {
            return (string)element.Element(name) ?? defaultValue;
        }

        public static DateTimeOffset ElementDateTimeOffset(this XElement element, XName name, DateTimeOffset defaultValue = default)
        {
            double? t = (double?)element.Element(name);

            if (t == null)
                return defaultValue;
            else
                return Utils.ConvertUnixTimeToDateTime(t.Value);
        }

        public static TimeSpan ElementTimeSpan(this XElement element, XName name, TimeSpan defaultValue = default)
        {
            double? t = (double?)element.Element(name);

            if (t == null)
                return defaultValue;
            else
                return Utils.ConvertSecondsToTimeSpan(t.Value);
        }
    }
}

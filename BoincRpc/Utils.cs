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

            foreach (byte b in hash)
                hashString.Append(b.ToString("x2"));

            return hashString.ToString();
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
                return DateTimeOffset.FromUnixTimeMilliseconds((long)(t.Value * 1000));
        }

        public static TimeSpan ElementTimeSpan(this XElement element, XName name, TimeSpan defaultValue = default)
        {
            double? t = (double?)element.Element(name);

            if (t == null)
                return defaultValue;
            else
                return TimeSpan.FromSeconds(t.Value);
        }
    }
}

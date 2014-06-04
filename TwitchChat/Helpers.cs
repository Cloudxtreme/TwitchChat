using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace TwitchChat
{
    static class Helpers
    {
        static string WildcardToRegex(string pattern)
        {
            return Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".");
        }

        public static bool IsWildcardMatch(this string self, string pattern)
        {
            Regex r = new Regex(WildcardToRegex(pattern), RegexOptions.IgnoreCase);
            return r.IsMatch(self);
        }

        public static TimeSpan Elapsed(this DateTime self)
        {
            return DateTime.UtcNow - self;
        }

        public static bool ParseBool(this string self, ref bool result)
        {
            if (bool.TryParse(self, out result))
                return true;

            result = true;
            if (self.Equals("true", StringComparison.CurrentCultureIgnoreCase))
                return true;
            else if (self.Equals("t", StringComparison.CurrentCultureIgnoreCase))
                return true;
            else if (self.Equals("yes", StringComparison.CurrentCultureIgnoreCase))
                return true;
            else if (self.Equals("y", StringComparison.CurrentCultureIgnoreCase))
                return true;
            else if (self.Equals("1", StringComparison.CurrentCultureIgnoreCase))
                return true;
            else if (self.Equals("enable", StringComparison.CurrentCultureIgnoreCase))
                return true;
            else if (self.Equals("enabled", StringComparison.CurrentCultureIgnoreCase))
                return true;
            else if (self.Equals("on", StringComparison.CurrentCultureIgnoreCase))
                return true;

            result = false;
            if (self.Equals("false", StringComparison.CurrentCultureIgnoreCase))
                return true;
            else if (self.Equals("f", StringComparison.CurrentCultureIgnoreCase))
                return true;
            else if (self.Equals("no", StringComparison.CurrentCultureIgnoreCase))
                return true;
            else if (self.Equals("n", StringComparison.CurrentCultureIgnoreCase))
                return true;
            else if (self.Equals("0", StringComparison.CurrentCultureIgnoreCase))
                return true;
            else if (self.Equals("disable", StringComparison.CurrentCultureIgnoreCase))
                return true;
            else if (self.Equals("disabled", StringComparison.CurrentCultureIgnoreCase))
                return true;
            else if (self.Equals("off", StringComparison.CurrentCultureIgnoreCase))
                return true;

            return false;
        }

        public static bool IsRegex(this string self)
        {
            bool firstQ = true;

            foreach (char c in self)
            {
                if ('a' <= c && c <= 'z')
                    continue;

                if ('A' <= c && c <= 'Z')
                    continue;

                if ('0' <= c && c <= '9')
                    continue;

                if (c == '.' || c == '/' || c == '_' || c == '-' || c == '=' || c == '&' || c == '%')
                    continue;

                // Heuristic, not going to be right all the time
                if (c == '?' && firstQ)
                {
                    firstQ = false;
                    continue;
                }

                return true;
            }

            return false;
        }
    }


    public static class NativeMethods
    {
        [DllImport("wininet.dll", SetLastError = true)]
        extern static bool InternetGetConnectedState(out int lpdwFlags, int dwReserved);

        public static bool IsConnectedToInternet()
        {
            int flags;
            return InternetGetConnectedState(out flags, 0);
        }
    }
}

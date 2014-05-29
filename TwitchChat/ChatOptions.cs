using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Winter;

namespace TwitchChat
{
    class ChatOptions
    {
        string m_stream, m_user, m_oath;
        string[] m_highlightList;
        HashSet<string> m_ignore = new HashSet<string>();
        IniReader m_iniReader;

        private RegistryKey m_reg;

        public string Stream { get { return m_stream; } }
        public string User { get { return m_user; } }
        public string Pass { get { return m_oath; } }

        public string[] Highlights { get { return m_highlightList; } }

        public HashSet<string> Ignore { get { return m_ignore; } }

        public ChatOptions()
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey("Software", true);
            m_reg = key.CreateSubKey("TwitchChatClient");

            m_iniReader = new IniReader("options.ini");

            IniSection section = m_iniReader.GetSectionByName("stream");
            if (section == null)
                throw new InvalidOperationException("Options file missing [Stream] section.");

            m_stream = section.GetValue("stream");
            m_user = section.GetValue("twitchname") ?? section.GetValue("user") ?? section.GetValue("username");
            m_oath = section.GetValue("oauth") ?? section.GetValue("pass") ?? section.GetValue("password");

            section = m_iniReader.GetSectionByName("highlight");
            List<string> highlights = new List<string>();
            if (section != null)
                foreach (string line in section.EnumerateRawStrings())
                    highlights.Add(DoReplacements(line.ToLower()));

            m_highlightList = highlights.ToArray();

            section = m_iniReader.GetSectionByName("ignore");
            if (section != null)
            
            m_ignore = new HashSet<string>((from s in section.EnumerateRawStrings()
                                            where !string.IsNullOrWhiteSpace(s)
                                            select s.ToLower()));
        }

        string DoReplacements(string value)
        {
            int i = value.IndexOf("$stream");
            while (i != -1)
            {
                value = value.Replace("$stream", m_stream);
                i = value.IndexOf("$stream");
            }
            return value;
        }

        internal bool GetOption(string key, bool defaultValue)
        {
            object res = m_reg.GetValue(key, defaultValue);
            if (res == null)
                return defaultValue;

            if (res is bool)
                return (bool)res;

            if (res is string)
                ((string)res).ParseBool(ref defaultValue);

            return defaultValue;
        }

        internal int GetOption(string key, int defaultValue)
        {
            object res = m_reg.GetValue(key, defaultValue);
            if (res == null)
                return defaultValue;

            if (res is int)
                return (int)res;

            int result;
            if (res is string)
                if (int.TryParse((string)res, out result))
                    return result;

            return defaultValue;
        }
        public void SetOption(string key, bool value)
        {
            m_reg.SetValue(key, value);
        }

        public void SetOption(string key, int value)
        {
            m_reg.SetValue(key, value);
        }
    }
}

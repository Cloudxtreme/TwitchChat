using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TwitchChat.JSON;

namespace TwitchChat
{
    class Emoticon
    {
        private System.Text.RegularExpressions.Regex m_reg;
        Task m_task;

        public string Regex { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public string Url { get; private set; }
        public string LocalFile { get; internal set; }
        public EmoticonSet ImageSet { get; private set; }

        public Emoticon(EmoticonSet set, string regex, JsonImage data)
        {
            Regex = regex;
            Width = data.width ?? -1;
            Height = data.height ?? -1;
            Url = data.url;
            ImageSet = set;

            if (Regex.IsRegex())
            {
                Regex = Regex.Replace(@"&gt\;", ">").Replace(@"&lt\;", "<");
                try
                {
                    m_reg = new Regex(Regex);
                }
                catch
                {
                }
            }

            string localPath = GetLocalPath(set.Cache);
            if (File.Exists(localPath))
                LocalFile = localPath;
        }

        public async Task Download()
        {
            string localFilePath = GetLocalPath(ImageSet.Cache);
            if (File.Exists(localFilePath))
                LocalFile = localFilePath;

            var task = m_task;
            if (task == null)
            {
                lock (this)
                {
                    if (m_task == null)
                        m_task = Download(localFilePath);

                    task = m_task;
                }
            }

            await task;
        }
        internal async void ForceRedownload()
        {
            Task task;
            lock (this)
            {
                task = m_task;
                m_task = null;
            }

            if (task != null)
                await task;

            string localFilePath = GetLocalPath(ImageSet.Cache);
            if (File.Exists(localFilePath))
            {
                try
                {
                    File.Delete(localFilePath);
                    LocalFile = null;
                }
                catch
                {
                    return;
                }
            }

            await Download();
        }

        private async Task Download(string localFilePath)
        {
            var request = (HttpWebRequest)HttpWebRequest.Create(new Uri(Url));
            var response = await Task<WebResponse>.Factory.FromAsync(request.BeginGetResponse, request.EndGetResponse, null);

            string directory = Path.GetDirectoryName(localFilePath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            try
            {
                using (FileStream fs = new FileStream(localFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                {
                    await response.GetResponseStream().CopyToAsync(fs);
                    LocalFile = localFilePath;
                }
            }
            catch (IOException)
            {
                lock (this)
                {
                    m_task = null;
                    LocalFile = null;
                }
            }
        }

        private string GetLocalPath(string cache)
        {
            int id = ImageSet.Id;
            return Path.Combine(cache, "emotes", id != -1 ? id.ToString() : "default", GetUrlFilename());
        }

        
        private string GetUrlFilename()
        {
            string filename = string.Empty;
            int i = Url.LastIndexOf('/');
            if (i > -1)
                filename = Url.Substring(i + 1);
            return filename;
        }

        internal IEnumerable<EmoticonFindResult> Find(string str)
        {
            if (m_reg != null)
            {
                var matches = m_reg.Matches(str);
                foreach (Match match in matches)
                    yield return new EmoticonFindResult(this, match.Index, match.Length);
            }
            else
            {
                int i = -1;

                while (i + Regex.Length <= str.Length)
                {
                    i = str.IndexOf(Regex, i + 1);
                    if (i == -1)
                        break;

                    yield return new EmoticonFindResult(this, i, Regex.Length);
                }
            }
        }

    }

    class EmoticonSet
    {
        EmoticonData m_data;
        Task m_task;
        List<Emoticon> m_emoticons = new List<Emoticon>();
        public int Id { get; private set; }

        public string Cache { get { return m_data.Cache; } }

        public EmoticonSet(EmoticonData data, int id)
        {
            m_data = data;
            Id = id;
        }

        public async Task DownloadAll()
        {
            var task = m_task;
            if (task != null)
            {
                await task;
                return;
            }

            lock (this)
                if (m_task == null)
                    m_task = GetDownloadTask();

            await m_task; 
        }

        Task GetDownloadTask()
        {
            var task = m_task;
            if (task != null)
                return task;

            Task[] tasks;
            lock (m_emoticons)
                tasks = (from emote in m_emoticons select emote.Download()).ToArray();

            return Task.WhenAll(tasks);
        }

        internal void Add(Emoticon e)
        {
            m_emoticons.Add(e);
        }

        internal IEnumerable<EmoticonFindResult> Find(string text)
        {
            foreach  (var emote in m_emoticons)
                foreach (var result in emote.Find(text))
                    yield return result;
        }
    }

    class EmoticonData
    {
        EmoticonSet m_defaultSet;
        List<EmoticonSet> m_sets = new List<EmoticonSet>();

        public string Cache { get; set; }

        public EmoticonSet DefaultEmoticons { get { return m_defaultSet; } }

        public EmoticonData(TwitchEmoticonResponse emotes, string cache)
        {
            m_defaultSet = new EmoticonSet(this, -1);
            Cache = cache;

            foreach (var emote in emotes.emoticons)
            {
                foreach (var img in emote.images)
                {
                    EmoticonSet set = GetOrCreateEmoticonSet(img.emoticon_set);
                    set.Add(new Emoticon(set, emote.regex, img));
                }
            }
        }

        public async void EnsureDownloaded(int[] sets)
        {
            if (sets == null)
                return;

            Task defaultTask = m_defaultSet.DownloadAll();

            var tasks = (from i in sets
                         let set = GetEmoticonSet(i)
                         where set != null
                         select set.DownloadAll()).ToArray();

            await defaultTask;
            await Task.WhenAll(tasks);
        }

        public EmoticonSet GetEmoticonSet(int id)
        {
            if (m_sets.Count <= id)
                return null;

            return m_sets[id];
        }

        public EmoticonSet GetEmoticonSet(int? id)
        {
            if (id == null)
                return m_defaultSet;

            int i = (int)id;
            if (m_sets.Count <= i)
                return null;

            return m_sets[i];
        }

        private EmoticonSet GetOrCreateEmoticonSet(int? id)
        {
            if (id == null)
                return m_defaultSet;

            int i = (int)id;
            while (m_sets.Count <= i)
                m_sets.Add(null);

            var set = m_sets[i];
            if (set == null)
                set = m_sets[i] = new EmoticonSet(this, i);

            return set;
        }

        internal IEnumerable<EmoticonFindResult> Find(string text, int[] sets)
        {
            if (sets != null)
            {
                foreach (var i in sets)
                {
                    var set = GetEmoticonSet(i);
                    if (set == null)
                        continue;

                    foreach (var item in set.Find(text))
                        yield return item;
                }
            }

            foreach (var item in m_defaultSet.Find(text))
                yield return item;
        }
    }

    class EmoticonFindResult
    {
        public Emoticon Emoticon { get; internal set; }
        public int Offset { get; internal set; }
        public int Length { get; internal set; }

        public EmoticonFindResult(Emoticon e, int offset, int len)
        {
            Emoticon = e;
            Offset = offset;
            Length = len;
        }
    }

    class LiveChannelData
    {
        public int CurrentViewerCount { get; private set; }

        public LiveChannelData(TwitchChannelResponse r)
        {
            CurrentViewerCount = r.channel_count;
        }
    }

    class TwitchApi
    {
        public static async Task<LiveChannelData> GetLiveChannelData(string channel)
        {
            string url = @"http://api.justin.tv/api/stream/list.json?channel=" + channel;
            TwitchChannelResponse[] data = null;

            try
            {
                var req = (HttpWebRequest)HttpWebRequest.Create(url);
                req.UserAgent = "TwitchChat Client";

                var response = await req.GetResponseAsync();
                var fromStream = response.GetResponseStream();

                StreamReader reader = new StreamReader(fromStream);

                data = JsonConvert.DeserializeObject<TwitchChannelResponse[]>(reader.ReadToEnd());
            }
            catch (Exception e)
            {
                Log.Instance.WebApiError(url, e.ToString());
            }

            if (data != null && data.Length > 0)
                return new LiveChannelData(data[0]);

            return null;
        }

        public static async Task<EmoticonData> GetEmoticonData(string cache)
        {
            string url = @"https://api.twitch.tv/kraken/chat/emoticons";
            TwitchEmoticonResponse emotes = null;

            for (int i = 0; i < 3 && emotes == null; i++)
            {
                try
                {
                    var req = (HttpWebRequest)HttpWebRequest.Create(url);
                    req.UserAgent = "TwitchChat Client";

                    var response = await req.GetResponseAsync();
                    var fromStream = response.GetResponseStream();

                    StreamReader reader = new StreamReader(fromStream);

                    emotes = JsonConvert.DeserializeObject<TwitchEmoticonResponse>(reader.ReadToEnd());
                }
                catch (Exception e)
                {
                    Log.Instance.WebApiError(url, e.ToString());
                }

                if (emotes == null)
                    await Task.Delay(30000);
            }

            var data = new EmoticonData(emotes, cache);
            data.DefaultEmoticons.DownloadAll();

            return data;
        }
    }
}
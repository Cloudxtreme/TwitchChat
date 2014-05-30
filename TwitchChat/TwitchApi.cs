using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TwitchChat.JSON;

namespace TwitchChat
{
    class Emoticon
    {
        private System.Text.RegularExpressions.Regex m_reg;

        public string Regex { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public string Url { get; private set; }
        public string LocalFile { get; private set; }
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
        }

        public async Task Download(string cache)
        {
            string localFilePath = GetLocalPath(cache);
            if (File.Exists(localFilePath))
                LocalFile = localFilePath;

            var client = new WebClient();
            var result = new TaskCompletionSource<string>();
            client.DownloadFileCompleted += (sender, e) =>
            {
                if (e.Error != null)
                    result.SetResult(LocalFile);
                else
                    result.SetResult(null);
            };
            
            client.DownloadFileAsync(new Uri(Url), localFilePath);
            LocalFile = await result.Task;
        }

        private string GetLocalPath(string cache)
        {
            int id = ImageSet.Id;

            string directory = Path.Combine(cache, "emotes", id != -1 ? id.ToString() : "default");
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            return Path.Combine(directory, GetUrlFilename());
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
        Task m_task;
        List<Emoticon> m_emoticons = new List<Emoticon>();
        public int Id { get; private set; }


        public async Task DownloadAll(string cache)
        {
            await GetDownloadTask(cache);
        }

        Task GetDownloadTask(string cache)
        {
            var task = m_task;
            if (task != null)
                return task;

            lock (m_emoticons)
            {
                var tasks = (from emote in m_emoticons select emote.Download(cache)).ToArray();
                task = Task.WhenAll(tasks);
            }

            return task;
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
        EmoticonSet m_defaultSet = new EmoticonSet();
        List<EmoticonSet> m_sets = new List<EmoticonSet>();
        private TwitchEmoticonResponse emotes;

        public EmoticonSet DefaultEmoticons { get { return m_defaultSet; } }

        public EmoticonData(TwitchEmoticonResponse emotes)
        {
            foreach (var emote in emotes.emoticons)
            {
                foreach (var img in emote.images)
                {
                    EmoticonSet set = GetOrCreateEmoticonSet(img.emoticon_set);
                    set.Add(new Emoticon(set, emote.regex, img));
                }
            }
        }

        public async void EnsureDownloaded(int[] sets, string cache)
        {
            if (sets == null)
                return;

            Task defaultTask = m_defaultSet.DownloadAll(cache);

            var tasks = (from i in sets
                         let set = GetEmoticonSet(i)
                         where set != null
                         select set.DownloadAll(cache)).ToArray();

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
                set = m_sets[i] = new EmoticonSet();

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

            var data = new EmoticonData(emotes);
            data.DefaultEmoticons.DownloadAll(cache);

            return data;
        }
    }
}
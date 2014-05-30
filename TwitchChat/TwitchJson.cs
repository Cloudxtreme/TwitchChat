using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;



namespace TwitchChat.JSON
{
    public class EmoticonResponseLinks
    {
        public string self { get; set; }
    }

    public class JsonImage
    {
        public int? width { get; set; }
        public int? height { get; set; }
        public string url { get; set; }
        public int? emoticon_set { get; set; }
    }

    public class EmoticonData
    {
        public string regex { get; set; }
        public List<JsonImage> images { get; set; }
    }

    public class TwitchEmoticonResponse
    {
        public EmoticonResponseLinks _links { get; set; }
        public List<EmoticonData> emoticons { get; set; }
    }



    public class Channel
    {
        public object subcategory { get; set; }
        public bool? producer { get; set; }
        public string image_url_huge { get; set; }
        public string timezone { get; set; }
        public string screen_cap_url_huge { get; set; }
        public int id { get; set; }
        public int views_count { get; set; }
        public string category { get; set; }
        public string embed_code { get; set; }
        public string title { get; set; }
        public string image_url_tiny { get; set; }
        public string screen_cap_url_large { get; set; }
        public string channel_url { get; set; }
        public string status { get; set; }
        public string meta_game { get; set; }
        public object tags { get; set; }
        public string image_url_small { get; set; }
        public string screen_cap_url_medium { get; set; }
        public string language { get; set; }
        public bool? embed_enabled { get; set; }
        public string subcategory_title { get; set; }
        public string image_url_medium { get; set; }
        public string image_url_large { get; set; }
        public bool? mature { get; set; }
        public string screen_cap_url_small { get; set; }
        public string login { get; set; }
        public string category_title { get; set; }
    }

    public class TwitchChannelResponse
    {
        public int broadcast_part { get; set; }
        public bool featured { get; set; }
        public bool channel_subscription { get; set; }
        public string id { get; set; }
        public string category { get; set; }
        public string title { get; set; }
        public int channel_count { get; set; }
        public int video_height { get; set; }
        public int site_count { get; set; }
        public bool? embed_enabled { get; set; }
        public Channel channel { get; set; }
        public string up_time { get; set; }
        public string meta_game { get; set; }
        public string format { get; set; }
        public int? embed_count { get; set; }
        public string stream_type { get; set; }
        public bool abuse_reported { get; set; }
        public int video_width { get; set; }
        public string geo { get; set; }
        public string name { get; set; }
        public string language { get; set; }
        public int stream_count { get; set; }
        public double video_bitrate { get; set; }
        public string broadcaster { get; set; }
        public int channel_view_count { get; set; }
    }
}

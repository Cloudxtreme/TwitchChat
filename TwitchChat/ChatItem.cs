using DarkAutumn.Twitch;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchChat
{
    public enum ItemType
    {
        Status,
        Subscriber,
        Question,
        Message,
        Action
    }

    public abstract class ChatItem
    {

        public MainWindow Controller { get; private set; }

        public event Action Clear;

        public TwitchChannel Channel { get; private set; }

        public ItemType Type { get; private set; }

        public TwitchUser User { get; protected set; }

        public ChatItem(TwitchChannel channel, MainWindow controller, ItemType type)
        {
            Channel = channel;
            Controller = controller;
            Type = type;
        }

        public void ClearChat()
        {
            var evt = Clear;
            if (evt != null)
                evt();
        }
    }

    public class Subscriber : ChatItem
    {
        public Subscriber(TwitchChannel channel, MainWindow controller, TwitchUser user)
            : base(channel, controller, ItemType.Subscriber)
        {
            User = user;
        }
    }

    public class ChatMessage : ChatItem
    {
        public string Message { get; private set; }

        public ChatMessage(TwitchChannel channel, MainWindow controller, TwitchUser user, string message, bool question)
            : base(channel, controller, question ? ItemType.Question : ItemType.Message)
        {
            User = user;
            Message = message;
        }

        public ChatMessage(TwitchChannel channel, MainWindow controller, ItemType type, TwitchUser user, string message)
            : base(channel, controller, type)
        {
            User = user;
            Message = message;
        }
    }

    public class ChatAction : ChatMessage
    {
        public ChatAction(TwitchChannel channel, MainWindow controller, TwitchUser user, string message)
            : base(channel, controller, ItemType.Action, user, message)
        {
        }
    }

    public class StatusMessage : ChatItem
    {
        public string Message { get; private set; }

        public StatusMessage(TwitchChannel channel, MainWindow controller, string message)
            : base(channel, controller, ItemType.Status)
        {
            Message = message;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Winter;

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
        public DateTime Time { get; private set; }

        public MainWindow Controller { get; private set; }

        public event Action Clear;

        public ItemType Type { get; private set; }

        public TwitchUser User { get; protected set; }

        public ChatItem(MainWindow controller, ItemType type)
        {
            Controller = controller;
            Type = type;
            Time = DateTime.Now;
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
        public Subscriber(MainWindow controller, TwitchUser user)
            : base(controller, ItemType.Subscriber)
        {
            User = user;
        }
    }

    public class ChatMessage : ChatItem
    {
        public string Message { get; private set; }

        public ChatMessage(MainWindow controller, TwitchUser user, string message, bool question)
            : base(controller, question ? ItemType.Question : ItemType.Message)
        {
            User = user;
            Message = message;
        }

        public ChatMessage(MainWindow controller, ItemType type, TwitchUser user, string message)
            : base(controller, type)
        {
            User = user;
            Message = message;
        }
    }

    public class ChatAction : ChatMessage
    {
        public ChatAction(MainWindow controller, TwitchUser user, string message)
            : base(controller, ItemType.Action, user, message)
        {
        }
    }

    public class StatusMessage : ChatItem
    {
        public string Message { get; private set; }

        public StatusMessage(MainWindow controller, string message)
            : base(controller, ItemType.Status)
        {
            Message = message;
        }
    }
}

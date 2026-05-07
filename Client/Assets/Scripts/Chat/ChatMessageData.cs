using System;
using System.Collections.Generic;

namespace Game.Chat
{
    [Serializable]
    public class ChatMessageData
    {
        public string Id          { get; set; }
        public string Channel     { get; set; }
        public string SenderId    { get; set; }
        public string SenderName  { get; set; }
        public string ReceiverId  { get; set; }
        public string Content     { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    [Serializable]
    public class ChatHistoryPayload
    {
        public string Channel        { get; set; }
        public string OtherPlayerId  { get; set; }
        public List<ChatMessageData> Messages { get; set; }
    }
}

using System;

[Serializable] // Dòng này cực kỳ quan trọng để Unity đọc được dữ liệu
public class FriendData
{
    public string playerId;
    public string playerName;
    public bool isOnline;
    public string lastSeenAt;
}
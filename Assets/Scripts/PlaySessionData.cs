using System;

[Serializable]
public class PlayEvent
{
    public float t;
    public string type;
    public int lane = -1;
    public int fromLane = -1;
    public int toLane = -1;
    public int questionId = -1;
    public int attemptNumber = -1;
    public int attemptsTotal = -1;
    public int bunnyLane = -1;
    public float reactionTimeMs = -1f;
    public string question;
    public string answer;
    public string obstacleName;
}

[Serializable]
public class PlaySession
{
    public string sessionId;
    public string sessionType;
    public string startedAt;
    public string endedAt;
    public string platform;
    public PlayEvent[] events;
}

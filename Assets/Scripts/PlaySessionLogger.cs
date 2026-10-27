using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class PlaySessionLogger : MonoBehaviour
{
    public static PlaySessionLogger Instance { get; private set; }

    readonly List<PlayEvent> events = new List<PlayEvent>();
    string sessionId;
    string startedAt;
    float sessionStartTime;
    bool saved;

    public static PlaySessionLogger Ensure()
    {
        if (Instance != null)
        {
            return Instance;
        }

        GameObject loggerObject = new GameObject("PlaySessionLogger");
        return loggerObject.AddComponent<PlaySessionLogger>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        BeginSession();
    }

    void BeginSession()
    {
        sessionStartTime = Time.realtimeSinceStartup;
        startedAt = DateTime.UtcNow.ToString("o");
        sessionId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        saved = false;
        events.Clear();
        Debug.Log("Play session started: " + sessionId);
    }

    /// <summary>
    /// Called when the run actually starts, so level-select and intro-video time is not
    /// counted in startedAt or in each event's t.
    /// </summary>
    public void RestartClock()
    {
        if (events.Count > 0 || saved)
        {
            return;
        }

        sessionStartTime = Time.realtimeSinceStartup;
        startedAt = DateTime.UtcNow.ToString("o");
    }

    public void LogLaneChange(int fromLane, int toLane)
    {
        PlayEvent playEvent = CreateEvent("lane_change");
        playEvent.fromLane = fromLane;
        playEvent.toLane = toLane;
        playEvent.lane = toLane;
        events.Add(playEvent);
    }

    public void LogShotFired(int lane)
    {
        PlayEvent playEvent = CreateEvent("shot_fired");
        playEvent.lane = lane;
        events.Add(playEvent);
    }

    public void LogShotCorrect(int lane, int questionId, string question, string answer)
    {
        PlayEvent playEvent = CreateEvent("shot_correct");
        playEvent.lane = lane;
        playEvent.questionId = questionId;
        playEvent.question = question;
        playEvent.answer = answer;
        events.Add(playEvent);
    }

    public void LogShotIncorrect(int lane, int questionId, string question, string answer)
    {
        PlayEvent playEvent = CreateEvent("shot_incorrect");
        playEvent.lane = lane;
        playEvent.questionId = questionId;
        playEvent.question = question;
        playEvent.answer = answer;
        events.Add(playEvent);
    }

    public void LogStimulusSpawned(int questionId, int laneIndex, int attemptNumber)
    {
        PlayEvent playEvent = CreateEvent("stimulus_spawned");
        playEvent.questionId = questionId;
        playEvent.lane = laneIndex;
        playEvent.attemptNumber = attemptNumber;
        events.Add(playEvent);
    }

    public void LogStimulusMissed(int questionId, int laneIndex, int attemptNumber, int bunnyLaneAtPass)
    {
        PlayEvent playEvent = CreateEvent("stimulus_missed");
        playEvent.questionId = questionId;
        playEvent.lane = laneIndex;
        playEvent.attemptNumber = attemptNumber;
        playEvent.bunnyLane = bunnyLaneAtPass;
        events.Add(playEvent);
    }

    public void LogStimulusAccepted(int questionId, int laneIndex, int attemptsTotal, float reactionTimeMs)
    {
        PlayEvent playEvent = CreateEvent("stimulus_accepted");
        playEvent.questionId = questionId;
        playEvent.lane = laneIndex;
        playEvent.attemptsTotal = attemptsTotal;
        playEvent.reactionTimeMs = reactionTimeMs;
        events.Add(playEvent);
    }

    public void LogQuestionShown(int questionId, string question)
    {
        PlayEvent playEvent = CreateEvent("question_shown");
        playEvent.questionId = questionId;
        playEvent.question = question;
        events.Add(playEvent);
    }

    public void LogCrash(int lane, string obstacleName)
    {
        PlayEvent playEvent = CreateEvent("crash");
        playEvent.lane = lane;
        playEvent.obstacleName = obstacleName;
        events.Add(playEvent);
        SaveSession();
    }

    public void SaveSession()
    {
        if (saved)
        {
            return;
        }

        PlaySession session = new PlaySession
        {
            sessionId = sessionId,
            sessionType = GameModeSettings.SessionType,
            startedAt = startedAt,
            endedAt = DateTime.UtcNow.ToString("o"),
            platform = Application.platform.ToString(),
            events = events.ToArray()
        };

        string folder = Path.Combine(Application.persistentDataPath, "session_logs");
        Directory.CreateDirectory(folder);

        string filePath = Path.Combine(folder, sessionId + ".json");
        File.WriteAllText(filePath, JsonUtility.ToJson(session, true));
        saved = true;

        Debug.Log("Play session saved (" + events.Count + " events): " + filePath);
    }

    void OnApplicationQuit()
    {
        SaveSession();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            SaveSession();
            Instance = null;
        }
    }

    PlayEvent CreateEvent(string type)
    {
        return new PlayEvent
        {
            t = Time.realtimeSinceStartup - sessionStartTime,
            type = type
        };
    }
}

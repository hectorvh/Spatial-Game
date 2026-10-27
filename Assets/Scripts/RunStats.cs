using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-run totals for the level-clear screen. Kept apart from PlaySessionLogger so the
/// session JSON format does not change. Times use scaled time, so pauses are excluded.
/// </summary>
public static class RunStats
{
    static readonly Dictionary<int, float> waveStartTimes = new Dictionary<int, float>();
    static readonly List<float> answerSeconds = new List<float>();
    static readonly List<float> stimulusReactionMs = new List<float>();

    public static int QuestionsPresented { get; private set; }
    public static int CorrectAnswers { get; private set; }
    public static int WrongShots { get; private set; }
    public static int ShotsFired { get; private set; }
    public static int Crashes { get; private set; }

    public static void Reset()
    {
        waveStartTimes.Clear();
        answerSeconds.Clear();
        stimulusReactionMs.Clear();
        QuestionsPresented = 0;
        CorrectAnswers = 0;
        WrongShots = 0;
        ShotsFired = 0;
        Crashes = 0;
    }

    public static void RecordAnswerWave(int questionId)
    {
        QuestionsPresented++;
        waveStartTimes[questionId] = Time.time;
    }

    public static void RecordCorrect(int questionId)
    {
        CorrectAnswers++;
        if (waveStartTimes.TryGetValue(questionId, out float start))
        {
            answerSeconds.Add(Time.time - start);
            waveStartTimes.Remove(questionId);
        }
    }

    public static void RecordWrongShot()
    {
        WrongShots++;
    }

    public static void RecordShot()
    {
        ShotsFired++;
    }

    public static void RecordCrash()
    {
        Crashes++;
    }

    public static void RecordStimulusReaction(float reactionTimeMs)
    {
        if (reactionTimeMs >= 0f)
        {
            stimulusReactionMs.Add(reactionTimeMs);
        }
    }

    public static float AnswerAccuracy => QuestionsPresented > 0 ? CorrectAnswers / (float)QuestionsPresented : 0f;

    public static float ShotAccuracy => ShotsFired > 0 ? CorrectAnswers / (float)ShotsFired : 0f;

    /// <summary>Answer wave spawn to correct hit, in seconds.</summary>
    public static bool TryMeanAnswerSeconds(out float mean)
    {
        return TryMean(answerSeconds, out mean);
    }

    /// <summary>Phase 1 token reaction time, Trial mode only.</summary>
    public static bool TryMeanStimulusReactionMs(out float mean)
    {
        return TryMean(stimulusReactionMs, out mean);
    }

    static bool TryMean(List<float> values, out float mean)
    {
        mean = 0f;
        if (values.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < values.Count; i++)
        {
            mean += values[i];
        }

        mean /= values.Count;
        return true;
    }
}

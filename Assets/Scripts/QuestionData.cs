using System;
using UnityEngine;

public enum StimulusMotionType
{
    Static,
    OrbitHorizontal,
    TraverseThrough,
    OscillateVertical
}

public enum SpatialRelation
{
    Inside,
    Under,
    OnTop,
    InFront,
    Behind,
    LeftOf,
    RightOf,
    Beside,
    Near,
    FarAway
}

[Serializable]
public class BoxDimensions
{
    public float width = 2f;
    public float height = 2.2f;
    public float depth = 1.5f;

    public Vector3 ToVector3()
    {
        return new Vector3(width, height, depth);
    }
}

[Serializable]
public class SpatialConfig
{
    public Vector3 sphereLocalPosition;
    public Vector3 sphereScale = new Vector3(0.6f, 0.6f, 0.6f);
    public BoxDimensions boxDimensions = new BoxDimensions();

    [Header("Path motion (optional)")]
    public bool isDynamic;
    public string motionType;
    public float speed = 2.5f;
    public Vector3 amplitude = new Vector3(2.2f, 0f, 1.4f);

    public StimulusMotionType ResolvedMotionType
    {
        get
        {
            if (!isDynamic || string.IsNullOrEmpty(motionType))
            {
                return StimulusMotionType.Static;
            }

            string text = motionType.Replace(" ", string.Empty);
            if (Enum.TryParse(text, true, out StimulusMotionType parsed))
            {
                return parsed;
            }

            Debug.LogWarning("SpatialConfig: unknown motionType '" + motionType + "', falling back to Static.");
            return StimulusMotionType.Static;
        }
    }

    public bool HasMotion => isDynamic && ResolvedMotionType != StimulusMotionType.Static;

    public bool IsValid =>
        boxDimensions != null &&
        boxDimensions.width > 0.01f &&
        boxDimensions.height > 0.01f &&
        boxDimensions.depth > 0.01f;

    public static SpatialConfig DefaultBox()
    {
        return new SpatialConfig
        {
            sphereLocalPosition = Vector3.zero,
            sphereScale = new Vector3(0.6f, 0.6f, 0.6f),
            boxDimensions = new BoxDimensions()
        };
    }

    public static SpatialConfig ForRelation(SpatialRelation relation)
    {
        SpatialConfig config = DefaultBox();
        switch (relation)
        {
            case SpatialRelation.Inside:
                config.sphereLocalPosition = Vector3.zero;
                break;
            case SpatialRelation.Under:
                config.sphereLocalPosition = new Vector3(0f, -1.5f, 0f);
                break;
            case SpatialRelation.OnTop:
                config.sphereLocalPosition = new Vector3(0f, 1.45f, 0f);
                break;
            case SpatialRelation.InFront:
                config.sphereLocalPosition = new Vector3(0f, 0f, -1.4f);
                break;
            case SpatialRelation.Behind:
                config.sphereLocalPosition = new Vector3(0f, 0f, 1.4f);
                break;
            case SpatialRelation.LeftOf:
                config.sphereLocalPosition = new Vector3(-1.8f, -0.5f, 0f);
                break;
            case SpatialRelation.RightOf:
                config.sphereLocalPosition = new Vector3(1.8f, -0.5f, 0f);
                break;
            case SpatialRelation.Beside:
                config.sphereLocalPosition = new Vector3(1.6f, 0f, 0f);
                break;
            case SpatialRelation.Near:
                config.sphereLocalPosition = new Vector3(1.15f, 0.1f, 0.25f);
                break;
            case SpatialRelation.FarAway:
                config.sphereLocalPosition = new Vector3(2.4f, 1.2f, 2.0f);
                break;
        }

        return config;
    }

    public static SpatialRelation InferRelation(string answer)
    {
        if (string.IsNullOrEmpty(answer))
        {
            return SpatialRelation.Inside;
        }

        string text = answer.ToLowerInvariant();
        if (text.Contains("inside"))
        {
            return SpatialRelation.Inside;
        }

        if (text.Contains("under"))
        {
            return SpatialRelation.Under;
        }

        if (text.Contains("on top") || text.Contains("top of"))
        {
            return SpatialRelation.OnTop;
        }

        if (text.Contains("in front") || text.Contains("front"))
        {
            return SpatialRelation.InFront;
        }

        if (text.Contains("behind"))
        {
            return SpatialRelation.Behind;
        }

        if (text.Contains("left"))
        {
            return SpatialRelation.LeftOf;
        }

        if (text.Contains("right"))
        {
            return SpatialRelation.RightOf;
        }

        if (text.Contains("near"))
        {
            return SpatialRelation.Near;
        }

        if (text.Contains("far"))
        {
            return SpatialRelation.FarAway;
        }

        if (text.Contains("beside"))
        {
            return SpatialRelation.Beside;
        }

        return SpatialRelation.Inside;
    }

    public static SpatialConfig Resolve(Question question)
    {
        if (question != null && question.spatialConfig != null && question.spatialConfig.IsValid)
        {
            SpatialConfig source = question.spatialConfig;
            if (source.sphereScale.sqrMagnitude < 0.0001f)
            {
                source.sphereScale = new Vector3(0.6f, 0.6f, 0.6f);
            }

            return source;
        }

        return ForRelation(InferRelation(question != null ? question.GetCorrectAnswer() : null));
    }
}

[Serializable]
public class Question
{
    public int id;
    public string question;
    public string[] answers;
    public int correctIndex;
    public string category;
    public int difficulty;
    public SpatialConfig spatialConfig;

    public string GetCorrectAnswer()
    {
        if (answers == null || answers.Length == 0)
        {
            return null;
        }

        int index = correctIndex;
        if (index < 0)
        {
            index = 0;
        }

        if (index >= answers.Length)
        {
            index = answers.Length - 1;
        }

        return answers[index];
    }
}

[Serializable]
public class QuestionList
{
    public Question[] questions;
}

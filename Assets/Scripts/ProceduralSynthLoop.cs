using System;
using UnityEngine;

/// <summary>
/// Generates a short synth-wave loop (Am - F - C - G, 112 BPM) used as level music
/// until a real track is assigned in the AudioLibrary. Every note tail wraps around
/// the buffer so the loop point is seamless.
/// </summary>
public static class ProceduralSynthLoop
{
    const int SampleRate = 22050;
    const float Bpm = 112f;
    const int Bars = 4;

    static readonly int[] Roots = { 45, 41, 48, 43 };
    static readonly int[][] Chords =
    {
        new[] { 57, 60, 64 },
        new[] { 53, 57, 60 },
        new[] { 60, 64, 67 },
        new[] { 55, 59, 62 }
    };
    static readonly int[] ArpPattern = { 0, 1, 2, 1 };

    public static AudioClip Build()
    {
        float beat = 60f / Bpm;
        float barLength = beat * 4f;
        int length = Mathf.RoundToInt(barLength * Bars * SampleRate);
        float[] buffer = new float[length];
        System.Random rng = new System.Random(7);

        for (int bar = 0; bar < Bars; bar++)
        {
            float barStart = bar * barLength;
            int[] chord = Chords[bar];

            for (int i = 0; i < chord.Length; i++)
            {
                AddPadVoice(buffer, barStart, barLength, chord[i]);
            }

            for (int i = 0; i < 8; i++)
            {
                int note = Roots[bar] + (i % 2 == 1 ? 12 : 0);
                AddBass(buffer, barStart + (i * beat * 0.5f), note);
            }

            for (int i = 0; i < 16; i++)
            {
                AddPluck(buffer, barStart + (i * beat * 0.25f), chord[ArpPattern[i % ArpPattern.Length]] + 12);
            }

            for (int b = 0; b < 4; b++)
            {
                float beatStart = barStart + (b * beat);
                AddKick(buffer, beatStart);
                AddHat(buffer, beatStart + (beat * 0.5f), rng);
                if (b == 1 || b == 3)
                {
                    AddSnare(buffer, beatStart, rng);
                }
            }
        }

        Master(buffer);

        AudioClip clip = AudioClip.Create("ProceduralSynthLoop", length, 1, SampleRate, false);
        clip.SetData(buffer, 0);
        return clip;
    }

    static double Frequency(int midiNote)
    {
        return 440.0 * Math.Pow(2.0, (midiNote - 69) / 12.0);
    }

    static void Render(float[] buffer, float startTime, float duration, Func<double, double> voice)
    {
        int start = Mathf.RoundToInt(startTime * SampleRate);
        int count = Mathf.RoundToInt(duration * SampleRate);
        for (int i = 0; i < count; i++)
        {
            double t = (double)i / SampleRate;
            buffer[(start + i) % buffer.Length] += (float)voice(t);
        }
    }

    static void AddBass(float[] buffer, float start, int note)
    {
        double w = 2.0 * Math.PI * Frequency(note);
        Render(buffer, start, 0.5f, t =>
        {
            double saw = 0.0;
            for (int k = 1; k <= 5; k++)
            {
                saw += Math.Sin(w * k * t) / k;
            }

            double env = Math.Min(1.0, t / 0.004) * Math.Exp(-t / 0.14);
            return saw * env * 0.2;
        });
    }

    static void AddPluck(float[] buffer, float start, int note)
    {
        double w = 2.0 * Math.PI * Frequency(note);
        Render(buffer, start, 0.4f, t =>
        {
            double tone = Math.Sin(w * t) + (0.3 * Math.Sin(2.0 * w * t)) + (0.15 * Math.Sin(3.0 * w * t));
            double env = Math.Min(1.0, t / 0.002) * Math.Exp(-t / 0.09);
            return tone * env * 0.05;
        });
    }

    static void AddPadVoice(float[] buffer, float start, float barLength, int note)
    {
        const float fade = 0.3f;
        double f = Frequency(note);
        double w1 = 2.0 * Math.PI * f * 1.0025;
        double w2 = 2.0 * Math.PI * f * 0.9975;
        float duration = barLength + fade;

        // Crossfades into the next bar's chord so changes do not click.
        Render(buffer, start, duration, t =>
        {
            double tone = Math.Sin(w1 * t) + Math.Sin(w2 * t) + (0.25 * Math.Sin(2.0 * w1 * t));
            double env = Math.Min(1.0, t / fade) * Math.Min(1.0, (duration - t) / fade);
            return tone * env * 0.03;
        });
    }

    static void AddKick(float[] buffer, float start)
    {
        Render(buffer, start, 0.45f, t =>
        {
            double phase = 2.0 * Math.PI * ((45.0 * t) + (70.0 * 0.03 * (1.0 - Math.Exp(-t / 0.03))));
            return Math.Sin(phase) * Math.Exp(-t / 0.16) * 0.55;
        });
    }

    static void AddSnare(float[] buffer, float start, System.Random rng)
    {
        Render(buffer, start, 0.3f, t =>
        {
            double noise = (rng.NextDouble() * 2.0) - 1.0;
            double body = Math.Sin(2.0 * Math.PI * 190.0 * t) * Math.Exp(-t / 0.05);
            return (noise * Math.Exp(-t / 0.07) * 0.14) + (body * 0.1);
        });
    }

    static void AddHat(float[] buffer, float start, System.Random rng)
    {
        double previous = 0.0;
        Render(buffer, start, 0.12f, t =>
        {
            double noise = (rng.NextDouble() * 2.0) - 1.0;
            double highPassed = noise - previous;
            previous = noise;
            return highPassed * Math.Exp(-t / 0.025) * 0.035;
        });
    }

    static void Master(float[] buffer)
    {
        float peak = 0f;
        for (int i = 0; i < buffer.Length; i++)
        {
            peak = Mathf.Max(peak, Mathf.Abs(buffer[i]));
        }

        if (peak <= 0f)
        {
            return;
        }

        const float drive = 1.5f;
        float scale = 1f / peak;
        float norm = (float)Math.Tanh(drive);
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = (float)Math.Tanh(buffer[i] * scale * drive) / norm * 0.85f;
        }
    }
}

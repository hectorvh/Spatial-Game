using System.Collections;
using UnityEngine;

/// <summary>
/// Shared collect/accept visual: spark burst plus a short scale-down tween.
/// </summary>
public static class PickupEffect
{
    public const float ScaleDuration = 0.22f;

    public static void PlayBurst(Vector3 position)
    {
        GameObject burstObject = new GameObject("PickupBurst");
        burstObject.transform.position = position;

        ParticleSystem particles = burstObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.35f;
        main.startLifetime = 0.45f;
        main.startSpeed = 4.5f;
        main.startSize = 0.12f;
        main.startColor = new Color(1f, 0.35f, 0.28f, 1f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 28;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 22) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.25f;

        ParticleSystemRenderer renderer = burstObject.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            Shader shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader != null)
            {
                renderer.sharedMaterial = new Material(shader)
                {
                    name = "PickupBurstParticle"
                };
            }
        }

        particles.Play();
        Object.Destroy(burstObject, 1.2f);
    }

    public static IEnumerator ScaleOut(Transform target, float duration = ScaleDuration)
    {
        if (target == null)
        {
            yield break;
        }

        Vector3 startScale = target.localScale;
        float elapsed = 0f;
        while (elapsed < duration && target != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - ((1f - t) * (1f - t));
            target.localScale = Vector3.Lerp(startScale, Vector3.zero, eased);
            yield return null;
        }

        if (target != null)
        {
            target.localScale = Vector3.zero;
        }
    }
}

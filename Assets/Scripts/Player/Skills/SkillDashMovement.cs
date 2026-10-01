using System;
using System.Collections;
using UnityEngine;

// Moves the actor; skill-specific hits and effects belong in onStep.
public static class SkillDashMovement
{
    public static IEnumerator Move(
        Transform actor, Vector3 direction, float distance, float duration,
        Action<Vector3, Vector3, float> onStep,
        Func<bool> isPaused = null, Func<bool> shouldStop = null)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= Mathf.Epsilon)
            direction = Vector3.forward;
        else
            direction.Normalize();

        Vector3 origin = actor.position;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (shouldStop != null && shouldStop())
                yield break;

            if (isPaused != null && isPaused())
            {
                yield return null;
                continue;
            }

            float step = Mathf.Min(Time.deltaTime, duration - elapsed);
            if (step <= 0f)
            {
                yield return null;
                continue;
            }

            Vector3 start = actor.position;
            elapsed += step;
            float traveled = distance * Mathf.Clamp01(elapsed / duration);
            Vector3 end = origin + direction * traveled;
            actor.position = end;
            onStep?.Invoke(start, end, traveled);
            yield return null;
        }
    }
}

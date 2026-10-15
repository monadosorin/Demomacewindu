using System.Collections;
using UnityEngine;

// Sits on the saber pivot (the hand). The blade points along local +Y.
public class Lightsaber : MonoBehaviour
{
    public enum Pose { Idle, Guard, Block, Reflect }

    public float poseSpeed = 14f;

    Quaternion targetRotation;
    Coroutine swingRoutine;

    public bool IsSwinging => swingRoutine != null;

    static Quaternion Rotation(Pose pose)
    {
        switch (pose)
        {
            case Pose.Guard: return Quaternion.Euler(30f, 0f, -10f);   // up, slightly forward
            case Pose.Block: return Quaternion.Euler(0f, 0f, 80f);     // horizontal across the body
            case Pose.Reflect: return Quaternion.Euler(-10f, 0f, 35f); // angled to bat lasers away
            default: return Quaternion.Euler(120f, 0f, 0f);            // low, pointing forward/down
        }
    }

    void Awake()
    {
        targetRotation = Rotation(Pose.Idle);
        transform.localRotation = targetRotation;
    }

    public void SetPose(Pose pose) => targetRotation = Rotation(pose);

    public void Swing(float duration)
    {
        if (swingRoutine != null) StopCoroutine(swingRoutine);
        swingRoutine = StartCoroutine(SwingRoutine(duration));
    }

    IEnumerator SwingRoutine(float duration)
    {
        var raised = Quaternion.Euler(-30f, 0f, -70f);
        var follow = Quaternion.Euler(110f, 0f, 70f);
        var start = transform.localRotation;

        float windup = duration * 0.35f;
        for (float t = 0f; t < windup; t += Time.deltaTime)
        {
            transform.localRotation = Quaternion.Slerp(start, raised, t / windup);
            yield return null;
        }

        float strike = duration * 0.65f;
        for (float t = 0f; t < strike; t += Time.deltaTime)
        {
            float k = t / strike;
            transform.localRotation = Quaternion.Slerp(raised, follow, k * k * (3f - 2f * k));
            yield return null;
        }

        swingRoutine = null;
    }

    void Update()
    {
        if (swingRoutine == null)
            transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, poseSpeed * Time.deltaTime);
    }
}

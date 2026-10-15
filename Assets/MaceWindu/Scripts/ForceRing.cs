using UnityEngine;

// Expanding (push) or collapsing (pull) neon ring used as the Force effect.
public class ForceRing : MonoBehaviour
{
    const int Segments = 40;

    LineRenderer line;
    float maxRadius;
    bool inward;
    float time;
    float duration = 0.4f;

    public static void Spawn(Vector3 center, float maxRadius, bool inward, Material material)
    {
        var go = new GameObject("ForceRing");
        go.transform.position = center;

        var line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.loop = true;
        line.useWorldSpace = false;
        line.positionCount = Segments;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var ring = go.AddComponent<ForceRing>();
        ring.line = line;
        ring.maxRadius = maxRadius;
        ring.inward = inward;
        ring.Apply(0f);
    }

    void Update()
    {
        time += Time.deltaTime;
        float k = Mathf.Clamp01(time / duration);
        Apply(k);
        if (k >= 1f) Destroy(gameObject);
    }

    void Apply(float k)
    {
        float radius = inward ? Mathf.Lerp(maxRadius, 0.2f, k) : Mathf.Lerp(0.2f, maxRadius, k);
        line.widthMultiplier = Mathf.Lerp(0.18f, 0f, k);
        for (int i = 0; i < Segments; i++)
        {
            float a = i * Mathf.PI * 2f / Segments;
            line.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
        }
    }
}

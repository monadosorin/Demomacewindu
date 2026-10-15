using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Health : MonoBehaviour
{
    public float maxHP = 100f;
    public Color flashColor = Color.white;

    public float HP { get; private set; }
    public bool IsDead => HP <= 0f;
    public float Normalized => HP / maxHP;

    public event Action<float> Damaged;
    public event Action Died;

    readonly List<Renderer> bodyRenderers = new List<Renderer>();
    MaterialPropertyBlock flashBlock;
    Coroutine flashRoutine;

    void Awake()
    {
        HP = maxHP;
        flashBlock = new MaterialPropertyBlock();
        flashBlock.SetColor("_BaseColor", flashColor);

        // Flash the grey box body only, not the saber blade
        foreach (var r in GetComponentsInChildren<Renderer>())
            if (r.GetComponentInParent<Lightsaber>() == null)
                bodyRenderers.Add(r);
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f) return;
        HP = Mathf.Max(0f, HP - amount);
        Damaged?.Invoke(amount);

        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(Flash());

        if (IsDead) Died?.Invoke();
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        HP = Mathf.Min(maxHP, HP + amount);
    }

    public void ResetHealth() => HP = maxHP;

    IEnumerator Flash()
    {
        foreach (var r in bodyRenderers) r.SetPropertyBlock(flashBlock);
        yield return new WaitForSeconds(0.08f);
        foreach (var r in bodyRenderers) r.SetPropertyBlock(null);
        flashRoutine = null;
    }
}

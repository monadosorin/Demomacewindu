using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// Deliberately simple opponent built on Unity's NavMeshAgent:
// wander -> chase -> saber swing up close / blaster shot at range -> flee when hurt.
[RequireComponent(typeof(NavMeshAgent), typeof(Health))]
public class EnemyAI : MonoBehaviour
{
    public enum AttackType { None, Saber, Laser }

    [Header("References")]
    public MaceWinduAI target;
    public Lightsaber saber;
    public Transform muzzle;
    public Material laserMaterial;

    [Header("Senses")]
    public float aggroRadius = 14f;
    public float wanderRadius = 14f;

    [Header("Saber")]
    public float meleeRange = 2.2f;
    public float meleeWindup = 0.6f;
    public float meleeCooldown = 1.6f;
    public float meleeDamage = 10f;
    [Range(0f, 1f)] public float parriedDamageFraction = 0.25f;

    [Header("Blaster")]
    public float minShootRange = 4.5f;
    public float shootRange = 12f;
    public float shootWindup = 0.7f;
    public float shootCooldown = 2.5f;
    public float laserDamage = 8f;

    [Header("Flee")]
    [Range(0f, 1f)] public float fleeBelowHp = 0.3f;
    public float fleeDuration = 2.5f;
    public float fleeCooldown = 12f;

    [Header("Movement")]
    public float wanderSpeed = 2f;
    public float chaseSpeed = 3.5f;
    public float fleeSpeed = 4.5f;

    public Health Health { get; private set; }
    public string ModeLabel { get; private set; } = "Wander";

    public AttackType CurrentAttack
    {
        get
        {
            if (Health.IsDead) return AttackType.None;
            if (windup != AttackType.None) return windup;
            if (activeBolt != null && !activeBolt.Reflected) return AttackType.Laser;
            return AttackType.None;
        }
    }

    public bool IsAttacking => CurrentAttack != AttackType.None;

    NavMeshAgent agent;
    AttackType windup;
    float windupTimer;
    float meleeReadyAt, shootReadyAt, fleeUntil, fleeReadyAt;
    LaserBolt activeBolt;
    bool stunned;
    Vector3 spawnPosition;
    Quaternion spawnRotation;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        Health = GetComponent<Health>();
        Health.Died += OnDied;
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
    }

    void Update()
    {
        if (Health.IsDead || stunned) return;

        if (windup != AttackType.None)
        {
            TickWindup();
            return;
        }

        bool targetAlive = target != null && !target.Health.IsDead;
        float distance = targetAlive ? FlatDistance(target.transform.position) : float.MaxValue;

        if (Time.time < fleeUntil)
        {
            Flee();
            return;
        }

        if (!targetAlive || distance > aggroRadius)
        {
            Wander();
            return;
        }

        if (Health.Normalized <= fleeBelowHp && Time.time >= fleeReadyAt)
        {
            fleeUntil = Time.time + fleeDuration;
            fleeReadyAt = Time.time + fleeCooldown;
            agent.ResetPath();
            return;
        }

        agent.updateRotation = false;
        Face(target.transform.position);

        if (distance <= meleeRange && Time.time >= meleeReadyAt)
        {
            StartWindup(AttackType.Saber, meleeWindup);
        }
        else if (distance >= minShootRange && distance <= shootRange && Time.time >= shootReadyAt && activeBolt == null)
        {
            StartWindup(AttackType.Laser, shootWindup);
        }
        else
        {
            ModeLabel = "Chase";
            agent.speed = chaseSpeed;
            agent.stoppingDistance = meleeRange * 0.8f;
            agent.SetDestination(target.transform.position);
        }
    }

    float FlatDistance(Vector3 point)
    {
        var d = point - transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    void Face(Vector3 point)
    {
        var d = point - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), 540f * Time.deltaTime);
    }

    void Wander()
    {
        ModeLabel = "Wander";
        agent.updateRotation = true;
        agent.speed = wanderSpeed;
        agent.stoppingDistance = 0.2f;
        saber.SetPose(Lightsaber.Pose.Idle);

        if (agent.pathPending || (agent.hasPath && agent.remainingDistance > 0.5f)) return;
        var random = Random.insideUnitSphere * wanderRadius;
        random.y = 0f;
        if (NavMesh.SamplePosition(random, out var hit, 2f, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
    }

    void Flee()
    {
        ModeLabel = "Flee";
        agent.updateRotation = true;
        agent.speed = fleeSpeed;
        agent.stoppingDistance = 0.2f;
        if (target == null) return;

        var away = transform.position + (transform.position - target.transform.position).normalized * 6f;
        if (NavMesh.SamplePosition(away, out var hit, 4f, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
    }

    void StartWindup(AttackType type, float duration)
    {
        windup = type;
        windupTimer = duration;
        agent.ResetPath();

        if (type == AttackType.Saber)
        {
            ModeLabel = "Saber Attack";
            saber.Swing(duration + 0.2f);
        }
        else
        {
            ModeLabel = "Aim Blaster";
            saber.SetPose(Lightsaber.Pose.Guard);
        }
    }

    void TickWindup()
    {
        if (target != null) Face(target.transform.position);
        windupTimer -= Time.deltaTime;
        if (windupTimer > 0f) return;

        var type = windup;
        windup = AttackType.None;

        if (target == null || target.Health.IsDead) return;

        if (type == AttackType.Saber)
        {
            meleeReadyAt = Time.time + meleeCooldown;
            if (FlatDistance(target.transform.position) > meleeRange + 0.6f) return;

            if (target.IsParrying)
            {
                target.Health.TakeDamage(meleeDamage * parriedDamageFraction);
                StartCoroutine(Stagger(0.5f));
            }
            else
            {
                target.Health.TakeDamage(meleeDamage);
            }
        }
        else
        {
            shootReadyAt = Time.time + shootCooldown;
            activeBolt = LaserBolt.Spawn(muzzle.position, target.transform, laserDamage, laserMaterial, gameObject);
        }
    }

    IEnumerator Stagger(float seconds)
    {
        stunned = true;
        ModeLabel = "Parried!";
        yield return new WaitForSeconds(seconds);
        stunned = false;
    }

    // Used by Mace's Force push / pull. Agent.Move keeps the enemy on the NavMesh.
    public void ApplyForceMove(Vector3 displacement, float duration, float stunAfter)
    {
        if (Health.IsDead || !agent.enabled) return;
        StopAllCoroutines();
        StartCoroutine(ForceMoveRoutine(displacement, duration, stunAfter));
    }

    IEnumerator ForceMoveRoutine(Vector3 displacement, float duration, float stunAfter)
    {
        stunned = true;
        windup = AttackType.None;
        ModeLabel = "Force Stunned";
        agent.ResetPath();

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            agent.Move(displacement * (Time.deltaTime / duration));
            yield return null;
        }

        yield return new WaitForSeconds(stunAfter);
        stunned = false;
    }

    void OnDied()
    {
        StopAllCoroutines();
        stunned = false;
        windup = AttackType.None;
        ModeLabel = "Dead";
        agent.ResetPath();
        agent.enabled = false;
        saber.SetPose(Lightsaber.Pose.Idle);
        transform.position += Vector3.up * 0.25f;
        transform.rotation *= Quaternion.Euler(-90f, 0f, 0f);
    }

    public void Respawn()
    {
        transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        agent.enabled = true;
        agent.Warp(spawnPosition);
        Health.ResetHealth();
        meleeReadyAt = shootReadyAt = fleeUntil = fleeReadyAt = 0f;
        ModeLabel = "Wander";
    }
}

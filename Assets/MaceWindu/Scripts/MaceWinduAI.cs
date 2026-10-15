using System;
using UnityEngine;
using UnityEngine.AI;

// Finite State Machine for Mace Windu, following Material/fsm.jpeg.
[RequireComponent(typeof(NavMeshAgent), typeof(Health))]
public class MaceWinduAI : MonoBehaviour
{
    public enum State
    {
        Idle, Patrol, Combat, Chase,
        Attack, AttackAgain, Retreat,
        Defend, Parry, ReflectLaser,
        ForcePush, ForcePull
    }

    [Header("References")]
    public EnemyAI enemy;
    public Lightsaber saber;
    public Transform[] patrolPoints;
    public Material forceMaterial;

    [Header("Senses")]
    public float idleDuration = 3f;
    public float detectRadius = 10f;

    [Header("HP thresholds (0-1)")]
    public float maceHighHp = 0.5f;
    public float maceLowHp = 0.35f;
    public float enemyLowHp = 0.4f;

    [Header("Saber")]
    public float attackRange = 2.3f;
    public float swingDuration = 0.55f;
    public float saberDamage = 12f;
    public float attackCooldown = 0.35f;

    [Header("Defence")]
    public float parryTimeout = 1.5f;
    public float reflectTimeout = 2.5f;
    public float reflectDamageMultiplier = 1.5f;

    [Header("Retreat")]
    public float retreatDistance = 5f;
    public float retreatDuration = 1.2f;

    [Header("Force")]
    public float pushDistance = 7f;
    public float pushDamage = 6f;
    public float pullStopDistance = 1.8f;

    [Header("Movement")]
    public float walkSpeed = 2.5f;
    public float combatSpeed = 3.5f;
    public float chaseSpeed = 5.5f;
    public float regenPerSecond = 4f;

    public State CurrentState => state;
    public float StateTime => stateTime;
    public Health Health { get; private set; }
    public bool IsParrying => state == State.Parry;

    // from, to, reason (the transition condition from the diagram)
    public event Action<State, State, string> StateChanged;

    NavMeshAgent agent;
    State state;
    float stateTime;
    int patrolIndex;
    bool actionDone;
    bool laserReflected;
    float nextAttackTime;
    Vector3 startPosition;
    Quaternion startRotation;

    public static string Label(State s)
    {
        switch (s)
        {
            case State.Combat: return "Mode Bertarung";
            case State.Attack: return "Attack State";
            case State.AttackAgain: return "Attack Lagi";
            case State.Defend: return "Defend State";
            case State.Parry: return "Tangkis";
            case State.ReflectLaser: return "Reflect Laser";
            case State.ForcePush: return "Push Musuh";
            case State.ForcePull: return "Pull Musuh";
            default: return s.ToString();
        }
    }

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        Health = GetComponent<Health>();
        Health.Died += OnDied;
        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    void Start() => ChangeState(State.Idle, "Start");

    // ---------------------------------------------------------------- helpers

    bool EnemyGone => enemy == null || enemy.Health.IsDead;
    bool MaceHpHigh => Health.Normalized >= maceHighHp;
    bool MaceHpLow => Health.Normalized <= maceLowHp;
    bool EnemyHpLow => enemy.Health.Normalized <= enemyLowHp;

    float DistanceToEnemy()
    {
        var d = enemy.transform.position - transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    Vector3 DirectionToEnemy()
    {
        var d = enemy.transform.position - transform.position;
        d.y = 0f;
        return d.sqrMagnitude > 0.0001f ? d.normalized : transform.forward;
    }

    void FaceEnemy()
    {
        var look = Quaternion.LookRotation(DirectionToEnemy());
        transform.rotation = Quaternion.RotateTowards(transform.rotation, look, 720f * Time.deltaTime);
    }

    bool EnemyDetected() => !EnemyGone && DistanceToEnemy() <= detectRadius;

    static bool IsCombatState(State s) => s != State.Idle && s != State.Patrol;

    void Stop()
    {
        if (agent.isOnNavMesh) agent.ResetPath();
    }

    void MoveTo(Vector3 destination, float speed, float stoppingDistance = 0.2f)
    {
        if (!agent.isOnNavMesh) return;
        agent.speed = speed;
        agent.stoppingDistance = stoppingDistance;
        agent.SetDestination(destination);
    }

    bool Arrived() =>
        agent.isOnNavMesh && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f;

    // ---------------------------------------------------------------- state machine

    void ChangeState(State next, string reason)
    {
        var previous = state;
        state = next;
        stateTime = 0f;
        actionDone = false;
        Enter(next);
        StateChanged?.Invoke(previous, next, reason);
    }

    void Enter(State s)
    {
        // The agent turns towards its path outside combat; in combat we face the enemy manually.
        agent.updateRotation = !IsCombatState(s) || s == State.Chase;

        switch (s)
        {
            case State.Idle:
                Stop();
                saber.SetPose(Lightsaber.Pose.Idle);
                break;

            case State.Patrol:
                saber.SetPose(Lightsaber.Pose.Idle);
                if (patrolPoints.Length > 0)
                    MoveTo(patrolPoints[patrolIndex % patrolPoints.Length].position, walkSpeed);
                break;

            case State.Combat:
            case State.Chase:
            case State.Defend:
            case State.ForcePush:
            case State.ForcePull:
                saber.SetPose(Lightsaber.Pose.Guard);
                if (s != State.Combat && s != State.Chase) Stop();
                break;

            case State.Attack:
            case State.AttackAgain:
                Stop();
                saber.Swing(swingDuration);
                break;

            case State.Retreat:
                saber.SetPose(Lightsaber.Pose.Guard);
                var away = transform.position - DirectionToEnemy() * retreatDistance;
                if (NavMesh.SamplePosition(away, out var hit, 3f, NavMesh.AllAreas))
                    MoveTo(hit.position, combatSpeed);
                break;

            case State.Parry:
                saber.SetPose(Lightsaber.Pose.Block);
                break;

            case State.ReflectLaser:
                laserReflected = false;
                saber.SetPose(Lightsaber.Pose.Reflect);
                break;
        }
    }

    void Update()
    {
        if (Health.IsDead) return;
        stateTime += Time.deltaTime;

        // "Musuh mati": any fighting state goes back to patrolling
        if (IsCombatState(state) && EnemyGone)
        {
            ChangeState(State.Patrol, "Musuh mati");
            return;
        }

        switch (state)
        {
            case State.Idle: UpdateIdle(); break;
            case State.Patrol: UpdatePatrol(); break;
            case State.Combat: UpdateCombat(); break;
            case State.Chase: UpdateChase(); break;
            case State.Attack: UpdateAttack(); break;
            case State.AttackAgain: UpdateAttackAgain(); break;
            case State.Retreat: UpdateRetreat(); break;
            case State.Defend: UpdateDefend(); break;
            case State.Parry: UpdateParry(); break;
            case State.ReflectLaser: UpdateReflect(); break;
            case State.ForcePush: UpdatePush(); break;
            case State.ForcePull: UpdatePull(); break;
        }
    }

    void UpdateIdle()
    {
        Health.Heal(regenPerSecond * Time.deltaTime);
        if (stateTime >= idleDuration)
            ChangeState(State.Patrol, "Setelah 3 detik");
    }

    void UpdatePatrol()
    {
        Health.Heal(regenPerSecond * Time.deltaTime);
        if (EnemyDetected())
        {
            ChangeState(State.Combat, "Musuh terdeteksi");
        }
        else if (Arrived())
        {
            patrolIndex++;
            ChangeState(State.Idle, "Sampai di titik stop");
        }
    }

    void UpdateCombat()
    {
        float distance = DistanceToEnemy();
        FaceEnemy();

        if (distance > detectRadius)
        {
            if (MaceHpHigh) ChangeState(State.Chase, "Musuh menjauh & HP Mace tinggi");
            else ChangeState(State.Patrol, "Musuh menjauh & HP Mace rendah");
        }
        else if (enemy.CurrentAttack == EnemyAI.AttackType.Laser ||
                 (enemy.CurrentAttack == EnemyAI.AttackType.Saber && distance <= attackRange + 1.5f))
        {
            ChangeState(State.Defend, "Musuh menyerang");
        }
        else if (distance <= attackRange && Time.time >= nextAttackTime)
        {
            ChangeState(State.Attack, "Jarak cukup & musuh tidak menyerang");
        }
        else
        {
            MoveTo(enemy.transform.position, combatSpeed, attackRange * 0.8f);
        }
    }

    void UpdateChase()
    {
        MoveTo(enemy.transform.position, chaseSpeed, attackRange * 0.8f);
        if (DistanceToEnemy() <= detectRadius * 0.8f)
            ChangeState(State.Combat, "Musuh terkejar");
        else if (stateTime > 8f)
            ChangeState(State.Patrol, "Kehilangan musuh");
    }

    // Returns true once the swing has finished.
    bool TickSwing()
    {
        FaceEnemy();
        if (!actionDone && stateTime >= swingDuration * 0.6f)
        {
            actionDone = true;
            if (DistanceToEnemy() <= attackRange + 0.6f)
                enemy.Health.TakeDamage(saberDamage);
        }

        if (stateTime < swingDuration) return false;
        nextAttackTime = Time.time + attackCooldown;
        return true;
    }

    void UpdateAttack()
    {
        if (!TickSwing() || EnemyGone) return;
        if (EnemyHpLow) ChangeState(State.AttackAgain, "HP musuh rendah");
        else ChangeState(State.Retreat, "HP musuh tinggi");
    }

    void UpdateAttackAgain()
    {
        if (!TickSwing() || EnemyGone) return;
        if (DistanceToEnemy() > attackRange + 1f) ChangeState(State.ForcePull, "Musuh menjauh");
        else ChangeState(State.Combat, "Else");
    }

    void UpdateRetreat()
    {
        FaceEnemy();
        if (MaceHpLow)
            ChangeState(State.ForcePush, "HP Mace rendah");
        else if (stateTime >= retreatDuration || Arrived())
            ChangeState(State.Combat, "Else");
    }

    void UpdateDefend()
    {
        FaceEnemy();
        switch (enemy.CurrentAttack)
        {
            case EnemyAI.AttackType.Saber: ChangeState(State.Parry, "Serangan saber"); break;
            case EnemyAI.AttackType.Laser: ChangeState(State.ReflectLaser, "Serangan laser"); break;
            default: ChangeState(State.Combat, "Serangan selesai"); break;
        }
    }

    void UpdateParry()
    {
        FaceEnemy();
        bool attackOver = enemy.CurrentAttack != EnemyAI.AttackType.Saber;
        if (!attackOver && stateTime < parryTimeout) return;

        if (MaceHpLow) ChangeState(State.ForcePush, "HP Mace rendah");
        else ChangeState(State.Combat, "Tangkis selesai");
    }

    void UpdateReflect()
    {
        FaceEnemy();
        bool attackOver = laserReflected || enemy.CurrentAttack != EnemyAI.AttackType.Laser;
        if (!attackOver && stateTime < reflectTimeout) return;

        if (DistanceToEnemy() > attackRange * 2f && MaceHpHigh)
            ChangeState(State.ForcePull, "Musuh jauh & HP Mace tinggi");
        else
            ChangeState(State.Combat, "Reflect selesai");
    }

    void UpdatePush()
    {
        FaceEnemy();
        if (!actionDone && stateTime >= 0.25f)
        {
            actionDone = true;
            enemy.ApplyForceMove(DirectionToEnemy() * pushDistance, 0.35f, 1f);
            enemy.Health.TakeDamage(pushDamage);
            ForceRing.Spawn(transform.position + Vector3.up * 1.1f, 3.5f, false, forceMaterial);
        }
        if (stateTime >= 0.8f)
            ChangeState(State.Combat, "Selesai push");
    }

    void UpdatePull()
    {
        FaceEnemy();
        if (!actionDone && stateTime >= 0.2f)
        {
            actionDone = true;
            float pull = Mathf.Max(0f, DistanceToEnemy() - pullStopDistance);
            enemy.ApplyForceMove(-DirectionToEnemy() * pull, 0.4f, 0.6f);
            ForceRing.Spawn(enemy.transform.position + Vector3.up * 1.1f, 2.5f, true, forceMaterial);
        }
        if (stateTime >= 0.7f)
            ChangeState(State.Combat, "Selesai pull");
    }

    // ---------------------------------------------------------------- called by other scripts

    public bool TryReflect(LaserBolt bolt)
    {
        if (state != State.ReflectLaser || EnemyGone) return false;
        laserReflected = true;
        bolt.Redirect(enemy.transform, enemy.laserDamage * reflectDamageMultiplier, gameObject);
        return true;
    }

    void OnDied()
    {
        Stop();
        agent.enabled = false;
        saber.SetPose(Lightsaber.Pose.Idle);
        transform.position += Vector3.up * 0.25f;
        transform.rotation *= Quaternion.Euler(-90f, 0f, 0f);
    }

    public void Respawn()
    {
        transform.SetPositionAndRotation(startPosition, startRotation);
        agent.enabled = true;
        agent.Warp(startPosition);
        Health.ResetHealth();
        patrolIndex = 0;
        ChangeState(State.Idle, "Respawn");
    }
}

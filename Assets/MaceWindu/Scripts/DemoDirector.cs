using System.Collections;
using UnityEngine;

// Keeps the demo looping: respawns the enemy after it dies, resets the round if Mace falls.
public class DemoDirector : MonoBehaviour
{
    public MaceWinduAI mace;
    public EnemyAI enemy;
    public float enemyRespawnDelay = 5f;
    public float roundResetDelay = 4f;

    public int MaceWins { get; private set; }
    public int EnemyWins { get; private set; }

    void Start()
    {
        enemy.Health.Died += () =>
        {
            MaceWins++;
            StartCoroutine(RespawnEnemy());
        };
        mace.Health.Died += () =>
        {
            EnemyWins++;
            StopAllCoroutines();
            StartCoroutine(ResetRound());
        };
    }

    IEnumerator RespawnEnemy()
    {
        yield return new WaitForSeconds(enemyRespawnDelay);
        if (!mace.Health.IsDead) enemy.Respawn();
    }

    IEnumerator ResetRound()
    {
        yield return new WaitForSeconds(roundResetDelay);
        enemy.Respawn();
        mace.Respawn();
    }
}

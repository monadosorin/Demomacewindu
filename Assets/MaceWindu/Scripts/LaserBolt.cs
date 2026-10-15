using UnityEngine;

public class LaserBolt : MonoBehaviour
{
    public float speed = 16f;
    public float lifetime = 3f;
    public float hitRadius = 0.7f;

    Vector3 direction;
    Transform target;
    float damage;

    public GameObject Owner { get; private set; }
    public bool Reflected { get; private set; }

    public static LaserBolt Spawn(Vector3 position, Transform target, float damage, Material material, GameObject owner)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "LaserBolt";
        Destroy(go.GetComponent<Collider>());
        go.transform.localScale = new Vector3(0.08f, 0.08f, 0.8f);
        go.GetComponent<Renderer>().sharedMaterial = material;

        var bolt = go.AddComponent<LaserBolt>();
        bolt.transform.position = position;
        bolt.Aim(target, damage, owner);
        return bolt;
    }

    public void Redirect(Transform newTarget, float newDamage, GameObject newOwner)
    {
        Reflected = true;
        lifetime = 3f;
        Aim(newTarget, newDamage, newOwner);
    }

    void Aim(Transform newTarget, float newDamage, GameObject newOwner)
    {
        target = newTarget;
        damage = newDamage;
        Owner = newOwner;
        direction = (AimPoint(target) - transform.position).normalized;
        transform.rotation = Quaternion.LookRotation(direction);
    }

    static Vector3 AimPoint(Transform t) => t.position + Vector3.up * 1.2f;

    void Update()
    {
        lifetime -= Time.deltaTime;
        if (lifetime <= 0f || target == null)
        {
            Destroy(gameObject);
            return;
        }

        transform.position += direction * speed * Time.deltaTime;

        if ((AimPoint(target) - transform.position).sqrMagnitude > hitRadius * hitRadius) return;

        var mace = target.GetComponent<MaceWinduAI>();
        if (mace != null && mace.TryReflect(this)) return;

        var health = target.GetComponent<Health>();
        if (health != null) health.TakeDamage(damage);
        Destroy(gameObject);
    }
}

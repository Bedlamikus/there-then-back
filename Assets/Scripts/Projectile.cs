using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Projectile : MonoBehaviour
{
    public float radius = 6f;
    public float maxDamage = 10f; // сколько наносим в центре сферы
    public bool destroyOnHit = true;

    void OnCollisionEnter(Collision c)
    {
        DoDamage(c.GetContact(0).point);
    }

    void OnTriggerEnter(Collider other)
    {
        DoDamage(transform.position);
    }

    void DoDamage(Vector3 hitPoint)
    {
        if (VoxelWorld.Instance != null)
            VoxelWorld.Instance.DamageSphere(hitPoint, radius, maxDamage);

        if (destroyOnHit) Destroy(gameObject);
    }
}

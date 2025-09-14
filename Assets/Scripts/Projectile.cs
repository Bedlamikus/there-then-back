using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Projectile : MonoBehaviour
{
    public float carveRadius = 6f; // радиус сферы в блоках/юнитах
    public bool destroyOnHit = true;

    void OnCollisionEnter(Collision collision)
    {
        TryCarve(collision.GetContact(0).point);
    }

    void OnTriggerEnter(Collider other)
    {
        // Если используешь триггеры вместо физ. столкновений
        TryCarve(transform.position);
    }

    void TryCarve(Vector3 hitPoint)
    {
        if (VoxelWorld.Instance != null)
        {
            VoxelWorld.Instance.CarveSphere(hitPoint, carveRadius);
        }

        if (destroyOnHit) Destroy(gameObject);
    }
}

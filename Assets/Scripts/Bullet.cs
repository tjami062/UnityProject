using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    [Header("Network Damage")]
    public int shooterId = -1;
    public int damage = 25;
    public float lifeTime = 3f;

    private void Start()
    {
        // Safety auto-destroy so bullets don't live forever
        Destroy(gameObject, lifeTime);
    }

    private void OnCollisionEnter(Collision objectWeHit)
    {
        // 1) NETWORKED HIT ON REMOTE PLAYER
        // -------------------------------------------------
        if (NetworkClient.Instance != null && NetworkClient.Instance.IsConnected)
        {
            // Check if we hit a RemotePlayer (other client)
            RemotePlayer rp = objectWeHit.collider.GetComponentInParent<RemotePlayer>();
            if (rp != null)
            {
                Debug.Log("Bullet hit remote player " + rp.playerId);

                if (shooterId != -1)
                {
                    // Tell server: HIT <targetId> <damage> <shooterId>
                    NetworkClient.Instance.Send($"HIT {rp.playerId} {damage} {shooterId}");
                }

                // Optional: impact effect on player (up to you)
                // CreateBulletImpactEffect(objectWeHit);

                Destroy(gameObject);
                return; // Don't continue to tag-based logic
            }
        }

        // 2) EXISTING SINGLE-PLAYER / ENVIRONMENT LOGIC
        // -------------------------------------------------

        if (objectWeHit.gameObject.CompareTag("Target"))
        {
            print("hit " + objectWeHit.gameObject.name + " !");

            CreateBulletImpactEffect(objectWeHit);

            Destroy(gameObject);
            return;
        }

        if (objectWeHit.gameObject.CompareTag("Wall"))
        {
            print("Hit a wall");

            CreateBulletImpactEffect(objectWeHit);

            Destroy(gameObject);
            return;
        }

        if (objectWeHit.gameObject.CompareTag("Beer"))
        {
            print("Hit a Beer Bottle");

            BeerBottle bottle = objectWeHit.gameObject.GetComponent<BeerBottle>();
            if (bottle != null)
            {
                bottle.Shatter();
            }

            // We will NOT destroy bullet here (per your original logic)
            return;
        }

        // Default: if we hit something else, just destroy the bullet
        Destroy(gameObject);
    }

    void CreateBulletImpactEffect(Collision objectWeHit)
    {
        ContactPoint contact = objectWeHit.contacts[0];

        GameObject hole = Instantiate(
            GlobalReferences.Instance.bulletImpactEffectPrefab,
            contact.point,
            Quaternion.LookRotation(contact.normal)
        );

        hole.transform.SetParent(objectWeHit.gameObject.transform);
    }
}

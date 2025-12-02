using UnityEngine;

public class Bullet : MonoBehaviour
{
    [Header("Network Damage")]
    public int shooterId = -1;
    public int damage = 25;
    public float lifeTime = 3f;

    [Header("Trail Materials")]
    public Material redTrail;
    public Material blueTrail;
    public Team shooterTeam;

    [Header("Impact Effect")]
    public GameObject impactEffect;

    // How long the detached trail stays alive
    [Header("Trail Persistence Settings")]
    public float trailLifetimeAfterHit = 10f;  // <-- make this as long as you want

    private TrailRenderer trail;

    private void Awake()
    {
        trail = GetComponent<TrailRenderer>();
    }

    private void Start()
    {
        ApplyTrailColor();
        Destroy(gameObject, lifeTime); // Bullet dies but trail stays alive
    }

    // ======================================
    // TRAIL COLOR BASED ON SHOOTER TEAM
    // ======================================

    public void ApplyTrailColor()
    {
        if (trail == null)
            trail = GetComponent<TrailRenderer>();

        if (trail != null)
        {
            trail.material = shooterTeam == Team.Red ? redTrail : blueTrail;
        }
    }

    // ======================================
    // DETACH TRAIL SO IT PERSISTS AFTER HIT
    // ======================================

    private void DetachTrail()
    {
        if (trail != null)
        {
            // Detach from bullet object so it can survive after bullet is destroyed
            trail.transform.SetParent(null);

            // Let the trail naturally fade for a LONG time
            Destroy(trail.gameObject, trailLifetimeAfterHit);
        }
    }

    // ======================================
    // COLLISION HANDLING
    // ======================================

    private void OnCollisionEnter(Collision hit)
    {
        // NETWORK PLAYER HIT
        RemotePlayer rp = hit.collider.GetComponentInParent<RemotePlayer>();
        if (rp != null)
        {
            if (shooterId != -1)
            {
                NetworkClient.Instance.Send($"HIT {rp.playerId} {damage} {shooterId}");
            }

            SpawnImpact(hit);
            DetachTrail();
            Destroy(gameObject);
            return;
        }

        // WALL OR TARGET
        if (hit.gameObject.CompareTag("Target") || hit.gameObject.CompareTag("Wall"))
        {
            SpawnImpact(hit);
            DetachTrail();
            Destroy(gameObject);
            return;
        }

        // BEER BOTTLE
        if (hit.gameObject.CompareTag("Beer"))
        {
            BeerBottle bottle = hit.gameObject.GetComponent<BeerBottle>();
            if (bottle != null)
            {
                bottle.Shatter();
            }

            DetachTrail();
            Destroy(gameObject);
            return;
        }

        // DEFAULT FALLBACK
        SpawnImpact(hit);
        DetachTrail();
        Destroy(gameObject);
    }

    // ======================================
    // IMPACT SPARK / EFFECT
    // ======================================

    private void SpawnImpact(Collision hit)
    {
        if (impactEffect == null)
            return;

        ContactPoint cp = hit.contacts[0];

        GameObject fx = Instantiate(
            impactEffect,
            cp.point,
            Quaternion.LookRotation(cp.normal)
        );

        fx.transform.SetParent(hit.collider.transform);
        Destroy(fx, 3f);
    }
}

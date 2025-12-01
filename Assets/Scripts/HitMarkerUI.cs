using System.Collections;
using UnityEngine;

public class HitMarkerUI : MonoBehaviour
{
    public static HitMarkerUI Instance { get; private set; }

    [Header("Fade Settings")]
    public float visibleTime = 0.15f;
    public float fadeOutTime = 0.1f;

    private CanvasGroup canvasGroup;
    private Coroutine currentRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // start hidden
        canvasGroup.alpha = 0f;
    }

    /// <summary>
    /// Call this when a hit is confirmed (e.g. from NetworkClient).
    /// </summary>
    public void ShowHitMarker()
    {
        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        currentRoutine = StartCoroutine(HitMarkerRoutine());
    }

    private IEnumerator HitMarkerRoutine()
    {
        // fully visible
        canvasGroup.alpha = 1f;

        // hold for a short time
        yield return new WaitForSeconds(visibleTime);

        // fade out
        float t = 0f;
        while (t < fadeOutTime)
        {
            t += Time.deltaTime;
            float normalized = Mathf.Clamp01(t / fadeOutTime);
            canvasGroup.alpha = 1f - normalized;
            yield return null;
        }

        canvasGroup.alpha = 0f;
        currentRoutine = null;
    }
}

using UnityEngine;

public class NetworkPositionSender : MonoBehaviour
{
    public float sendInterval = 0.05f;

    private float _timer;

    private void Update()
    {
        _timer += Time.deltaTime;
        if (_timer >= sendInterval)
        {
            _timer = 0f;

            if (NetworkClient.Instance != null && NetworkClient.Instance.IsConnected)
            {
                Vector3 pos = transform.position;
                Vector3 euler = transform.eulerAngles;
                NetworkClient.Instance.SendPosition(pos, euler);
            }
        }
    }
}
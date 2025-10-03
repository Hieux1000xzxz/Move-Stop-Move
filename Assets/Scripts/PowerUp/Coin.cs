using UnityEngine;

public class Coin : MonoBehaviour
{
    [SerializeField] private int value = 100;
    private bool isCollected;
    private Collider col;

    private void Awake()
    {
        col = GetComponent<Collider>();
    }

    private void OnEnable()
    {
        isCollected = false;
        if (col != null) col.enabled = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isCollected) return;
        if (!other.CompareTag("Player")) return;
        
        isCollected = true;
        
        if (col != null) col.enabled = false;
        gameObject.SetActive(false);  

        CoinManager.Instance.AddCoin(value);

        ObjectPool.Instance.ReleaseCoin(gameObject);
    }
}
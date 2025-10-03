using UnityEngine;

public class Coin : MonoBehaviour
{
    [SerializeField] private int value = 100;
    private bool isCollected;
    [SerializeField] private Collider col;

    private void OnEnable()
    {
        isCollected = false;
        if (col != null) col.enabled = true;
    }

   
    private void OnTriggerEnter(Collider other)
    {
        if (isCollected) return;

        CharacterBase character = other.GetComponent<CharacterBase>();
        if (character == null) return;

        if (character.ownerType == CharacterBase.OwnerType.Player && character.IsOwner)
        {
            isCollected = true;
            if (col != null) col.enabled = false;

            CoinManager.Instance.AddCoin(value);
            ObjectPool.Instance.ReleaseCoin(gameObject);
        }
    }
}
using System.Collections;
using UnityEngine;
using Unity.Netcode;

public class Powerup : NetworkBehaviour
{
    [Header("Config")]
    [SerializeField] public PowerupType type;
    [SerializeField] public float duration = 5f;

    [Header("Refs")]
    [SerializeField] public Collider triggerCollider;
    [SerializeField] public NetworkObject netObject;

    public System.Action OnReleased;

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        CharacterBase character = other.GetComponent<CharacterBase>();
        if (character != null)
        {
            // ❌ bỏ StartCoroutine trên server
            // ✅ chỉ gọi RPC để mọi client (kể cả host) tự xử lý
            character.ApplyPowerupClientRpc(type, duration);

            ObjectPool.Instance.ReleasePowerup(gameObject, type);
        }
    }
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (triggerCollider != null)
        {
            triggerCollider.enabled = IsServer;
        }
    }
    private IEnumerator ApplySpeedBoost(CharacterBase character, float duration, float multiplier = 2f, float fadeTime = 2f)
    {
        float oldSpeed = character.moveSpeed;
        float boostedSpeed = oldSpeed * multiplier;

        character.moveSpeed = boostedSpeed;
        if (character.agent != null)
            character.agent.speed = boostedSpeed;

        yield return new WaitForSeconds(duration - fadeTime);

        float elapsed = 0f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeTime;

            float newSpeed = Mathf.Lerp(boostedSpeed, oldSpeed, t);
            character.moveSpeed = newSpeed;

            if (character.agent != null)
                character.agent.speed = newSpeed;

            yield return null;
        }

        character.moveSpeed = oldSpeed;
        if (character.agent != null)
            character.agent.speed = oldSpeed;
    }


    private IEnumerator ApplyWeaponGrow(CharacterBase character, float duration, float scaleMultiplier = 1.5f)
    {
        if (character.currentWeaponPublic == null) yield break;

        Transform weaponTransform = character.currentWeaponPublic.transform;
        Vector3 oldScale = weaponTransform.localScale;

        weaponTransform.localScale = oldScale * scaleMultiplier;

        yield return new WaitForSeconds(duration);

        if (character.currentWeaponPublic != null)
            weaponTransform.localScale = oldScale;
    }

    public void SetType(PowerupType newType)
    {
        type = newType;
    }

    private void OnDisable()
    {
        OnReleased?.Invoke();
        OnReleased = null;
    }
    private new void OnDestroy()
    {
        foreach (var obj in ObjectPool.Instance.preAllocations)
        {
            var powerup = obj.gameObject.GetComponent<Powerup>();
            if (powerup != null)
            {
                powerup.OnReleased = null;
            }
        }
    }

}

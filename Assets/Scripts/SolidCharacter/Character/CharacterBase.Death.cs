using UnityEngine;
using System.Collections;
using Unity.Netcode;

public partial class CharacterBase
{
    #region Death

    protected void CheckForDead()
    {
        if (health.IsDead && !hasDied)
        {
            hasDied = true;
            isDead = true;

            HandleDeathCleanup();
            HandleDeathAnimation();
            scoreDisplay.gameObject.SetActive(false);
            if (!IsServer)
            {
                NotifyServerOfDeathServerRpc();
            }
            else
            {
                StartCoroutine(DeathSequenceCoroutine());
            }
        }
    }

    private IEnumerator DeathSequenceCoroutine()
    {
        float deathAnimTime = 1.0f;
        yield return new WaitForSeconds(deathAnimTime);

        if (IsServer && GameManager.Instance != null)
        {
            if (characterCollider != null)
                characterCollider.enabled = false;

            GameManager.Instance.HandleCharacterDeath(this);
            SpawnCoinUniversal();
        }

        yield return new WaitForSeconds(0.1f);
        if (networkObject != null && networkObject.IsSpawned)
            networkObject.Despawn(true);
    }


    private void HandleDeathCleanup()
    {
        StopAllCoroutines();
        scoreDisplay.gameObject.SetActive(false);
        characterCollider.enabled = false;

        if (AgentValid)
        {
            agent.enabled = false;
            agent.velocity = Vector3.zero;
        }

        if (currentWeapon != null)
        {
            HideOrReleaseWeapon();
        }
    }

    private void HandleDeathAnimation()
    {
        TriggerDeathAnimation();
        if (IsServer)
            DisableColliderClientRpc();
    }

    private void SpawnCoinUniversal()
    {
        GameObject coin = ObjectPool.Instance.SpawnCoin(transform.position + Vector3.up, Quaternion.identity);
        coin.GetComponent<Coin>().SetOwner(this);
    }

    [ServerRpc]
    private void NotifyServerOfDeathServerRpc()
    {
        StartCoroutine(DeathSequenceCoroutine());
    }

    #endregion
}
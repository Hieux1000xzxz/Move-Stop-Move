using UnityEngine;
using System.Collections;
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
            if (IsServer)
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
            //agent.ResetPath();
        }
    }

    private void HandleDeathAnimationLocal()
    {
        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
            SetAttackAnim(false);
            animator.CrossFade("Death", 0.1f);
        }
    }

    private void HandleDeathAnimation()
    {
        HandleDeathAnimationLocal(); 
        if (IsServer)
        {
            DisableColliderClientRpc();
            PlayDeathAnimationClientRpc();
        }
    }

    private void ReleaseWeaponOnDeath()
    {
        if (currentWeapon == null) return;
        
        HideOrReleaseWeapon();
        
        if (IsServer)
            ObjectPool.Instance.ReleaseWeapon(currentWeapon.gameObject);
        else
            currentWeapon.gameObject.SetActive(false);

        currentWeapon = null;
    }

    private IEnumerator DelayedDisable(float delay)
    {
        yield return new WaitForSeconds(delay);
        gameObject.SetActive(false);
    }

    private void SpawnCoinUniversal()
    {
        GameObject coin = ObjectPool.Instance.SpawnCoin(transform.position + Vector3.up, Quaternion.identity);
        coin.GetComponent<Coin>().SetOwner(this);
    }

    #endregion
}

using UnityEngine;
using Unity.Netcode;

public partial class CharacterBase
{
    #region Score & Stats

    private void OnScoreChanged(int oldValue, int newValue)
    {
        if (scoreDisplay != null)
        {
            scoreDisplay.SetScore(newValue);
            UpdateCharacterStats();
        }
    }

    //sync Score
    public void AddScore(int value)
    {
        if (!IsServer) return;
        Score.Value += value;
    }

    private void UpdateCharacterStats()
    {
        if (scoreDisplay == null) return;

        UpdateScale();
        UpdateAttackRange();
        UpdateMoveSpeed();
    }

    private void UpdateScale()
    {
        float newScale = Mathf.Min(1f + scoreDisplay.CurrentScore * sizePerScore, maxScale);
        transform.localScale = Vector3.one * newScale;

        if (currentWeapon != null)
        {
            currentWeapon.ApplyScale(newScale);
        }
    }

    private void UpdateAttackRange()
    {
        attackRange += scoreDisplay.CurrentScore * rangePerScore;
    }

    private void UpdateMoveSpeed()
    {
        moveSpeed += moveSpeedPerScore;

        if (AgentValid)
        {
            agent.speed = moveSpeed;
        }
    }

    public void ResetState()
    {
        ResetCoreState();
        ResetHealth();
        ResetUIAndCollider();
        ResetWeapon();
        ResetAnimator();
        ResetAgent();

        hasDied = false;
    }

    private void ResetCoreState()
    {
        currentState = CharacterState.Idle;
        attackTarget = null;
        detectedTarget = null;
        isAttacking = false;
        isDead = false;
        hasWeapon = true;
        scoreDisplay.gameObject.SetActive(true);
    }

    private void ResetHealth()
    {
        if (IsServer && health != null)
        {
            health.CurrentHealth.Value = health.MaxHealth;
        }
    }

    private void ResetUIAndCollider()
    {
        scoreDisplay.gameObject.SetActive(true);
        gameObject.layer = LayerMask.NameToLayer("Player");
        characterCollider.enabled = true;
    }

    private void ResetWeapon()
    {
        if (IsServer)
        {
            ChangeWeapon(weaponType);
        }
    }

    private void ResetAnimator()
    {
        if (networkAnimator != null && networkAnimator.Animator != null)
        {
            networkAnimator.Animator.SetFloat("Speed", 0f);
        }

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }
    }

    private void ResetAgent()
    {
        agent.enabled = true;
    }

    #endregion

    #region Weapons Handling

    public void ChangeWeapon(WeaponType newWeaponType)
    {
        if (!IsServer) return;

        ReleaseCurrentWeapon();

        WeaponBase weapon = ObjectPool.Instance.SpawnWeaponByOwner(this, newWeaponType);
        if (weapon == null) return;

        NetworkObject netObj = weapon.NetworkObj;
        SetupNewWeapon(weapon, netObj);
    }

    private void ReleaseCurrentWeapon()
    {
        if (currentWeapon == null) return;

        ObjectPool.Instance.ReleaseWeapon(currentWeapon.gameObject);
        currentWeapon.ClearOwner();
        currentWeapon = null;
    }

    private void SetupNewWeapon(WeaponBase weapon, NetworkObject netObj)
    {
        if (weapon == null || netObj == null) return;

        currentWeapon = weapon;
        currentWeapon.Init(this, weaponSpawnPoint);
        currentWeapon.SetOwner(this);

        if (!netObj.IsSpawned)
            netObj.Spawn(true);

        NetCurrentWeapon.Value = netObj;
        AssignWeapon(currentWeapon);
        SetWeaponClientRpc(netObj, this.networkObject);
    }

    private void HideOrReleaseWeapon()
    {
        if (currentWeapon == null) return;

        if (ownerType == OwnerType.Player)
        {
            currentWeapon.gameObject.SetActive(false);
        }
        else
        {
            if (IsServer)
                ObjectPool.Instance.ReleaseWeapon(currentWeapon.gameObject);
            else
                currentWeapon.gameObject.SetActive(false);
        }

        currentWeapon = null;
    }

    public void AssignWeapon(WeaponBase weapon)
    {
        currentWeapon = weapon;
        currentWeapon.Init(this, weaponSpawnPoint);
    }

    #endregion
}
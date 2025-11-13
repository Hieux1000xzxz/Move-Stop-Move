using UnityEngine;
using Unity.Netcode;
using System.Collections;
using Unity.Collections;

public partial class CharacterBase
{
    #region Network Methods

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        SetupWeaponSync();
        SetupInitialWeapon();
        SetupPlayerInfo();
        SetupScoreDisplay();
        SetupScoreSync();

        if (!IsOwner)
        {
            SetupAnimationSyc();
        }

        SetupCoinSync();
    }

    private void SetupAnimationSyc()
    {
        netSpeed.OnValueChanged += (oldVal, newVal) =>
        {
            if (animator != null && !(this is Player))
            {
                animator.SetFloat("Speed", newVal);
            }
        };

        netIsAttacking.OnValueChanged += (oldVal, newVal) =>
        {
            if (animator != null && !(this is Player))
            {
                animator.SetBool("IsAttacking", newVal);
            }
        };

        if (animator != null && !(this is Player))
        {
            SetAnimatorParameters(netSpeed.Value, netIsAttacking.Value);
        }

        NetState.OnValueChanged += (oldVal, newVal) => { currentState = newVal; };
    }

    private void SetupCoinSync()
    {
        SessionCoin.OnValueChanged += (oldVal, newVal) =>
        {
            if (IsOwner && CoinManager.Instance != null)
            {
                CoinManager.Instance.UpdateCoinUIFromSession(newVal);
            }
        };
    }

    private void SetupWeaponSync()
    {
        NetCurrentWeapon.OnValueChanged += (oldVal, newVal) =>
        {
            if (newVal.TryGet(out NetworkObject obj))
            {
                currentWeapon = WeaponBase.GetByNetworkObject(obj);
                currentWeapon.SetOwner(this);
            }
            else
            {
                currentWeapon = null;
            }
        };

        StartCoroutine(ResolveCurrentWeaponInitial());
    }

    private void SetupInitialWeapon()
    {
        if (NetCurrentWeapon.Value.TryGet(out NetworkObject objNow))
        {
            currentWeapon = WeaponBase.GetByNetworkObject(objNow);
            currentWeapon.SetOwner(this);
        }

        if (IsOwner)
        {
            string savedWeapon = PlayerPrefs.GetString("SelectedWeapon", WeaponType.Knife1.ToString());

            if (!System.Enum.TryParse(savedWeapon, out WeaponType weaponType))
                weaponType = WeaponType.Knife1;

            RequestSetWeaponServerRpc(weaponType);
        }
    }

    private void SetupPlayerInfo()
    {
        if (IsOwner && ownerType == OwnerType.Player)
        {
            string localName = PlayerPrefs.GetString("PlayerName", "Player");
            SubmitPlayerNameServerRpc(localName);
        }
    }

    private void SetupScoreDisplay()
    {
        if (scoreDisplay != null)
        {
            scoreDisplay.SetScore(Score.Value);

            PlayerName.OnValueChanged += (oldName, newName) => { scoreDisplay.SetPlayerName(newName.ToString()); };

            scoreDisplay.SetPlayerName(PlayerName.Value.ToString());
        }
    }

    private void SetupScoreSync()
    {
        Score.OnValueChanged += OnScoreChanged;

        if (scoreDisplay != null)
        {
            scoreDisplay.SetScore(Score.Value);
            UpdateCharacterStats();
        }
    }

    private IEnumerator ResolveCurrentWeaponInitial()
    {
        yield return null;

        float timeout = 5f;
        float t = 0f;

        while (t < timeout && currentWeapon == null)
        {
            if (TryResolveWeaponFromNetworkObject())
                yield break;

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (IsServer && currentWeapon == null)
        {
            ChangeWeapon(weaponType);
        }
    }

    private bool TryResolveWeaponFromNetworkObject()
    {
        if (NetCurrentWeapon.Value.TryGet(out NetworkObject obj) && obj != null)
        {
            var weap = WeaponBase.GetByNetworkObject(obj);
            if (weap != null)
            {
                currentWeapon = weap;
                currentWeapon.SetOwner(this);
                AssignWeapon(currentWeapon);
                return true;
            }
        }

        return false;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        CleanupScoreEvents();
        CleanupWeaponReferences();
        CleanupPlayerNameSync();
    }

    private void CleanupScoreEvents()
    {
        Score.OnValueChanged -= OnScoreChanged;

        Score.OnValueChanged -= (oldValue, newValue) =>
        {
            scoreDisplay.SetScore(newValue);
            UpdateCharacterStats();
        };
    }

    private void CleanupWeaponReferences()
    {
        if (IsServer && currentWeapon != null)
        {
            ObjectPool.Instance.ReleaseWeapon(currentWeapon.gameObject);
            currentWeapon.ClearOwner();
        }
    }

    private void CleanupPlayerNameSync()
    {
        if (IsOwner && ownerType == OwnerType.Player)
        {
            string localName = PlayerPrefs.GetString("PlayerName", "Player");
            SubmitPlayerNameServerRpc(localName);
        }
    }

    [ServerRpc]
    private void SubmitPlayerNameServerRpc(string newName)
    {
        PlayerName.Value = new FixedString32Bytes(newName);
    }

    [ServerRpc]
    public void RequestSetWeaponServerRpc(WeaponType selectedWeapon)
    {
        ChangeWeapon(selectedWeapon);
    }

    [ClientRpc]
    private void SetWeaponClientRpc(NetworkObjectReference weaponRef, NetworkObjectReference ownerRef)
    {
        if (TryGetNetworkObjects(weaponRef, ownerRef, out NetworkObject weaponObj, out NetworkObject ownerObj))
        {
            WeaponBase weapon = WeaponBase.GetByNetworkObject(weaponObj);
            CharacterBase ownerChar = ownerObj.TryGetComponent(out CharacterBase ch) ? ch : null;

            HandleWeaponAssignment(weapon, ownerChar);
        }
    }

    private void HandleWeaponAssignment(WeaponBase weapon, CharacterBase ownerChar)
    {
        if (weapon != null && ownerChar != null)
        {
            ownerChar.AssignWeapon(weapon);
            weapon.SetOwner(ownerChar);
        }
    }

    private bool TryGetNetworkObjects(NetworkObjectReference weaponRef,
        NetworkObjectReference ownerRef,
        out NetworkObject weaponObj,
        out NetworkObject ownerObj)
    {
        weaponObj = null;
        ownerObj = null;

        bool hasWeapon = weaponRef.TryGet(out weaponObj);
        bool hasOwner = ownerRef.TryGet(out ownerObj);

        return hasWeapon && hasOwner;
    }


    [ClientRpc]
    private void DisableColliderClientRpc()
    {
        if (characterCollider != null)
        {
            characterCollider.enabled = false;
        }
    }

    #endregion
}
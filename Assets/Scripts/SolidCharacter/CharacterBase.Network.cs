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
        
        /*NetState.OnValueChanged += (oldVal, newVal) =>
        {
            currentState = newVal; // sync local state with server
        };*/
        
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

            PlayerName.OnValueChanged += (oldName, newName) =>
            {
                scoreDisplay.SetPlayerName(newName.ToString());
            };

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
            if (NetCurrentWeapon.Value.TryGet(out NetworkObject obj) && obj != null)
            {
                var weap = WeaponBase.GetByNetworkObject(obj);
                if (weap != null)
                {
                    currentWeapon = weap;
                    currentWeapon.SetOwner(this);
                    AssignWeapon(currentWeapon);
                    yield break;
                }
            }

            t += Time.unscaledDeltaTime;
            yield return null;
        }
        
        if (IsServer && currentWeapon == null)
        {
            ChangeWeapon(weaponType);
        }
    }
    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        
        Score.OnValueChanged -= OnScoreChanged;
        
        if (IsServer && currentWeapon != null)
        {
            ObjectPool.Instance.ReleaseWeapon(currentWeapon.gameObject);
            currentWeapon.ClearOwner();
   
        }
        
        if (IsOwner && ownerType == OwnerType.Player)
        {
            string localName = PlayerPrefs.GetString("PlayerName", "Player");
            SubmitPlayerNameServerRpc(localName);
        }
        Score.OnValueChanged -= (oldValue, newValue) =>
        {
            Debug.Log($"[CLIENT] {gameObject.name} Score synced {oldValue} -> {newValue}");
            scoreDisplay.SetScore(newValue);
            UpdateCharacterStats();
        };
    }
    [ServerRpc]
    private void SubmitPlayerNameServerRpc(string newName)
    {
        PlayerName.Value = new FixedString32Bytes(newName);
    }
    //Anim Attack

    [ServerRpc]
    public void RequestSetWeaponServerRpc(WeaponType selectedWeapon)
    {
        ChangeWeapon(selectedWeapon);
    }

    [ClientRpc]
    private void SetWeaponClientRpc(NetworkObjectReference weaponRef, NetworkObjectReference ownerRef)
    {
        if (weaponRef.TryGet(out NetworkObject weaponObj) &&
            ownerRef.TryGet(out NetworkObject ownerObj))
        {
            var weapon = WeaponBase.GetByNetworkObject(weaponObj);
            var ownerChar = ownerObj.TryGetComponent(out CharacterBase ch) ? ch : null;

            if (weapon != null && ownerChar != null)
            {
                ownerChar.AssignWeapon(weapon);
                weapon.SetOwner(ownerChar);
            }
        }
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

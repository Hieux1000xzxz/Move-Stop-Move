using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
public partial class ConnectionCanvas
{
    #region LobbyList
    public void RefreshLobbyList()
    {
        if (this != null)
            StartCoroutine(RefreshLobbyListRoutine());
    }

    private IEnumerator RefreshLobbyListRoutine()
    {
        yield return StartCoroutine(GetRequest("list", (json) =>
            {
                RelayLobbyInfo[] lobbies = JsonHelper.FromJson<RelayLobbyInfo>(json);
                ClearContainerCache(cachedLobbyItems);
                UpdateLobbyListUI(lobbies);
            },
            (error) =>
            {
                SendNotification($"Failed to refresh lobby list: {error}", 1);
            }));
    }
    
    private void UpdateLobbyListUI(RelayLobbyInfo[] lobbies)
    {
        ClearContainerCache(cachedLobbyItems);
        for (int i = 0; i < lobbies.Length; i++)
        {
            var item = GetOrCreateCachedItem(cachedLobbyItems, lobbyItemPrefab, lobbyListContainer, i);
            if (item != null)
                item.Setup(lobbies[i], this);
        }
    }

    private IEnumerator AutoRefreshLobbyList()
    {
        while (true)
        {
            if (mainPanel.activeInHierarchy)
                SafeRefreshLobby();
            yield return new WaitForSeconds(3f);
        }
    }
    #endregion
}

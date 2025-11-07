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
        using (var www = UnityWebRequest.Get($"{SERVER_URL}/list"))
        {
            www.timeout = 10;
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                string json = www.downloadHandler.text;
                RelayLobbyInfo[] lobbies = JsonHelper.FromJson<RelayLobbyInfo>(json);
                ClearContainerCache(cachedLobbyItems);

                for (int i = 0; i < lobbies.Length; i++)
                {
                    var lobbyItem = GetOrCreateCachedItem(cachedLobbyItems, lobbyItemPrefab, lobbyListContainer, i);
                    if (lobbyItem != null)
                        lobbyItem.Setup(lobbies[i], this);
                }
            }
        }
    }
    private IEnumerator AutoRefreshLobbyList()
    {
        while (true)
        {
            if (mainPanel.activeInHierarchy)
            {
                RefreshLobbyList();
            }
            yield return new WaitForSeconds(3f);
        }
    }
    #endregion
}

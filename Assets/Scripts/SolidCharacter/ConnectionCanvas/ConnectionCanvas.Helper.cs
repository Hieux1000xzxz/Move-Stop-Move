using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using System.Text.RegularExpressions;
using System.Text;

public partial class ConnectionCanvas
{
    #region Utility & Helpers
    private T GetOrCreateCachedItem<T>(List<T> itemCache, T prefab, Transform parent, int index) where T : Component
    {
        if (index < itemCache.Count && itemCache[index] != null)
        {
            itemCache[index].gameObject.SetActive(true);
            return itemCache[index];
        }
        
        var newItem = Instantiate(prefab, parent);
        if (index >= itemCache.Count) itemCache.Add(newItem);
        else
        {
                itemCache[index] = newItem;
        }
        return newItem;
    }
    private void ClearContainerCache<T>(List<T> itemCache) where T : Component
    {
        foreach (var item in itemCache)
        {
            if (item != null && item.gameObject != null)
                item.gameObject.SetActive(false);
        }
    }
    private void OnBackToModePanel()
    {
        if (isCreatingRoom || isJoiningRoom) return;

        mainPanel.SetActive(false);
        modePanel.SetActive(true);
    }
    public void DisableMainPanelButton()
    {
        startHostButton.interactable = false;
        joinByIdButton.interactable = false;
        backButton.interactable = false;
    }
    public void EnableMainPanelButton()
    {
        startHostButton.interactable = true;
        joinByIdButton.interactable = true;
        backButton.interactable = true;
    }
    private void CloseJoinByIdPanel()
    {
        if (joinByIdPanel != null)
        {
            joinByIdPanel.SetActive(false);
            EnableAllJoinButtons();
            EnableMainPanelButton();
        }
    }
    private IEnumerator ExecuteAsync(Func<Task> asyncMethod)
    {
        var task = asyncMethod();
        yield return new WaitUntil(() => task.IsCompleted);
    }
    private void HandleClientDisconnect()
    {
        if (!string.IsNullOrEmpty(currentLobbyId) && !string.IsNullOrEmpty(localUserName))
        {
            string playerId = PlayerPrefs.GetString("PlayerId", "");
            if (!string.IsNullOrEmpty(playerId))
            {
                var user = new UserInfo { userId = playerId, userName = localUserName };
                StartCoroutine(LeaveLobbyRoutine(currentLobbyId, user));
            }
        }
        ResetState();
        SetUIState(true);
        SafeRefreshLobby();
    }
    private void OnBackToMenu()
    {
        lobbyPanel.SetActive(false);
        if (joinByIdPanel != null) joinByIdPanel.SetActive(false);
        GameManager.Instance.ShowPlayerPreview();
        UIManager.Instance.CloseNotification();
        UIManager.Instance.CloseNetwork();
        UIManager.Instance.OpenMainMenu();
    }
    private void ClearContainer(Transform container)
    {
        if (container == null) return;
        for (int i = container.childCount - 1; i >= 0; i--)
            Destroy(container.GetChild(i).gameObject);
    }
    private IEnumerator StartGameOnServerRoutine()
    {
        if (string.IsNullOrEmpty(currentLobbyId)) yield break;

        var emptyPayload = new { };

        using (var www = CreatePostRequest($"{SERVER_URL}/{currentLobbyId}/start", emptyPayload))
        {
            yield return www.SendWebRequest();
        }
    }
    private void SendNotification(string message, int type)
    {
        UIManager.Instance.SendNotification(message, type);
        UIManager.Instance.OpenNotification();
    }
    private void ResetNetworkManager()
    {
        if (networkManager == null) return;

        if (networkManager.IsListening)
            networkManager.Shutdown();

        if (transport != null)
            transport.SetConnectionData("127.0.0.1", 7777);

        StartCoroutine(ResetNetworkManagerCoroutine());
    }
    private IEnumerator ResetNetworkManagerCoroutine()
    {
        yield return null;

        if (networkManager != null)
        {
            networkManager.gameObject.SetActive(false);
            yield return null;
            networkManager.gameObject.SetActive(true);
        }
    }
    private void DisableAllJoinButtons()
    {
        foreach (var lobbyItem in cachedLobbyItems)
        {
            if (lobbyItem != null && lobbyItem.gameObject.activeSelf)
                lobbyItem.SetJoinButtonInteractable(false);
        }
    }
    private void EnableAllJoinButtons()
    {
        foreach (var lobbyItem in cachedLobbyItems)
        {
            if (lobbyItem != null && lobbyItem.gameObject.activeSelf)
                lobbyItem.SetJoinButtonInteractable(true);
        }
    }
    private void CancelJoinProcess()
    {
        if (isJoiningRoom)
        {
            isJoiningRoom = false;
            if (joinLobbyCoroutine != null)
            {
                StopCoroutine(joinLobbyCoroutine);
                joinLobbyCoroutine = null;
            }
            UIManager.Instance.CloseNotification();
            EnableAllJoinButtons();
            EnableMainPanelButton();
        }
    }
    
    private bool IsValidString(string input, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            SendNotification($"{fieldName} cannot be empty", 1);
            return false;
        }
        if (!Regex.IsMatch(input, @"^[a-zA-Z0-9 ]+$"))
        {
            SendNotification($"Please use only letters and numbers for {fieldName}.", 1);
            return false;
        }
        return true;
    }

    private IEnumerator EnsureServerAvailable(Action<bool> onChecked)
    {
        bool available = false;
        yield return StartCoroutine(CheckServerAvailabilityCoroutine(result => available = result));
        if (!available)
            SendNotification("Server unavailable. Please try again later.", 1);
        onChecked?.Invoke(available);
    }
    private IEnumerator CheckServerAvailabilityCoroutine(System.Action<bool> callback)
    {
        yield return StartCoroutine(GetRequest("ping", (text) => callback(true), (err) => callback(false)));
    }
    private void SafeRefreshLobby()
    {
        if (this != null && !isCreatingRoom && !isJoiningRoom)
            RefreshLobbyList();
    }
   
    #endregion
}

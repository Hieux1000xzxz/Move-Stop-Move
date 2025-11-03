using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class PlayerSelectionSync : NetworkBehaviour
{
    public static PlayerSelectionSync Instance;
    private readonly Dictionary<ulong, string> selectedCharacters = new();

    private void Awake()
    {
        Instance = this;
    }

    [ServerRpc(RequireOwnership = false)]
    public void SendSelectedCharacterServerRpc(string selectedCharacter, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        selectedCharacters[clientId] = selectedCharacter;
        Debug.Log($"[Server] Client {clientId} chọn nhân vật: {selectedCharacter}");
    }

    public string GetSelectedCharacter(ulong clientId)
    {
        if (selectedCharacters.TryGetValue(clientId, out string charName))
            return charName;

        return string.Empty;
    }
}
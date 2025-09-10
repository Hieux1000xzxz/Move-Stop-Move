using Unity.Netcode;
using UnityEngine;

public class GameNetworkManager : MonoBehaviour
{
    [SerializeField] private NetworkManager networkManager;

    private void Awake()
    {
        networkManager.ConnectionApprovalCallback += ApprovalCheck;
    }

    public void StartHost()
    {
        networkManager.StartHost();
        Debug.Log("Host started");
    }

    public void StartClient()
    {
        networkManager.StartClient();
        Debug.Log("Client started");
    }

    public void StartServer()
    {
        networkManager.StartServer();
        Debug.Log("Server started");
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        ulong clientId = request.ClientNetworkId;

        Vector3 spawnPos = Vector3.zero;

        if (clientId == 0)
        {
            spawnPos = new Vector3(-10f, 0f, 0f);
        }
        else if (clientId == 1) 
        {
            spawnPos = new Vector3(10f, 0f, 0f);
        }
        else
        {
            float angle = (clientId - 1) * 90f;
            float radius = 15f;
            spawnPos = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad),
                                   0f,
                                   Mathf.Sin(angle * Mathf.Deg2Rad)) * radius;
        }

        response.Approved = true;
        response.CreatePlayerObject = true;   
        response.Position = spawnPos;
        response.Rotation = Quaternion.identity;
    }
}

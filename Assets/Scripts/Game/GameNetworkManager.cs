using Unity.Netcode;
using UnityEngine;

public class GameNetworkManager : MonoBehaviour
{
    [SerializeField] private NetworkManager networkManager;

    void Start()
    {
       
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
}
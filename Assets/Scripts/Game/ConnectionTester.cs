using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using TMPro;

public class ConnectionTester : NetworkBehaviour
{
    [SerializeField] private TextMeshProUGUI connectionInfoText;

    private void Update()
    {
        UpdateConnectionInfo();
    }

    private void UpdateConnectionInfo()
    {
        if (NetworkManager.Singleton == null) return;

        string info = "";

        if (NetworkManager.Singleton.IsServer)
        {
            info += "Máy: Host Server\n";
            info += $"Số client đã kết nối: {NetworkManager.Singleton.ConnectedClients.Count}\n";
            info += $"Địa chỉ: {GetLocalIPAddress()}\n";
            info += "Port: 7777";
        }
        else if (NetworkManager.Singleton.IsClient)
        {
            info += "Máy: Client\n";
            info += $"Đã kết nối đến: {NetworkManager.Singleton.GetComponent<UnityTransport>().ConnectionData.Address}\n";
            info += $"Client ID: {NetworkManager.Singleton.LocalClientId}";
        }
        else
        {
            info += "Chưa kết nối\n";
            info += $"Địa chỉ IP local: {GetLocalIPAddress()}";
        }

        connectionInfoText.text = info;
    }

    private string GetLocalIPAddress()
    {
        var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                return ip.ToString();
            }
        }
        return "127.0.0.1";
    }
}
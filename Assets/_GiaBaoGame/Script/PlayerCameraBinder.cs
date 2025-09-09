using Unity.Netcode;
using UnityEngine;
using Unity.Cinemachine;

public class PlayerCameraBinder : NetworkBehaviour
{
    private void Start()
    {
        if (IsOwner)
        {
            var vcam = FindFirstObjectByType<CinemachineCamera>();
            if (vcam != null)
            {
                vcam.Follow = transform;
                vcam.LookAt = transform;
            }
        }
    }
}

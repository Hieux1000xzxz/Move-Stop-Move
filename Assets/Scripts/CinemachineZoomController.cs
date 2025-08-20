using UnityEngine;
using Unity.Cinemachine;

public class CinemachineZoomController : MonoBehaviour
{
    [SerializeField] private CinemachineCamera virtualCamera;
    [SerializeField] private KillScoreDisplay scoreDisplay;
    [SerializeField] private float baseFOV = 60f;
    [SerializeField] private float fovPerScore = 2f;
    [SerializeField] private float maxFOV = 90f;
    [SerializeField] private float zoomSpeed = 2f;

    [Header("Follow Offset Settings")]
    [SerializeField] private float baseFollowY = 5f;
    [SerializeField] private float followYPerScore = 0.5f;
    [SerializeField] private float maxFollowY = 15f;
    [SerializeField] private CinemachineFollow transposer;


    private void Update()
    {
        if (scoreDisplay == null || virtualCamera == null || transposer == null) return;

        // --- FOV ---
        float targetFOV = baseFOV + (scoreDisplay.CurrentScore * fovPerScore);
        targetFOV = Mathf.Min(targetFOV, maxFOV);

        virtualCamera.Lens.FieldOfView = Mathf.Lerp(
            virtualCamera.Lens.FieldOfView,
            targetFOV,
            Time.deltaTime * zoomSpeed
        );

        // --- Follow Offset Y ---
        float targetFollowY = baseFollowY + (scoreDisplay.CurrentScore * followYPerScore);
        targetFollowY = Mathf.Min(targetFollowY, maxFollowY);

        Vector3 currentOffset = transposer.FollowOffset;
        Vector3 targetOffset = new Vector3(currentOffset.x, targetFollowY, currentOffset.z);

        transposer.FollowOffset = Vector3.Lerp(
            currentOffset,
            targetOffset,
            Time.deltaTime * zoomSpeed
        );
    }
}

using UnityEngine;
using Unity.Cinemachine;
using System.Collections;

public class CinemachineZoomController : MonoBehaviour
{
    [SerializeField] private CinemachineCamera virtualCamera;
    [SerializeField] public float baseFOV = 60f;
    [SerializeField] private float fovPerScore = 2f;
    [SerializeField] private float maxFOV = 90f;
    [SerializeField] private float zoomSpeed = 2f;

    [Header("Follow Offset Settings")]
    [SerializeField] public float baseFollowY = 5f;
    [SerializeField] private float followYPerScore = 0.5f;
    [SerializeField] private float maxFollowY = 15f;
    [SerializeField] private CinemachineFollow transposer;

    private KillScoreDisplay scoreDisplay;
    private Coroutine zoomRoutine;

    public void SetUp(KillScoreDisplay killScore)
    {
        if (scoreDisplay != null)
            scoreDisplay.OnScoreChanged -= HandleScoreChanged;

        scoreDisplay = killScore;

        if (scoreDisplay != null)
            scoreDisplay.OnScoreChanged += HandleScoreChanged;

        HandleScoreChanged(scoreDisplay.CurrentScore);
    }

    private void HandleScoreChanged(int newScore)
    {
        float targetFOV = Mathf.Min(baseFOV + (newScore * fovPerScore), maxFOV);
        float targetFollowY = Mathf.Min(baseFollowY + (newScore * followYPerScore), maxFollowY);

        if (zoomRoutine != null) StopCoroutine(zoomRoutine);
        zoomRoutine = StartCoroutine(SmoothZoom(targetFOV, targetFollowY));
    }

    private IEnumerator SmoothZoom(float targetFOV, float targetFollowY)
    {
        while (virtualCamera != null && transposer != null &&
               (Mathf.Abs(virtualCamera.Lens.FieldOfView - targetFOV) > 0.01f ||
                Mathf.Abs(transposer.FollowOffset.y - targetFollowY) > 0.01f))
        {
            virtualCamera.Lens.FieldOfView = Mathf.Lerp(
                virtualCamera.Lens.FieldOfView,
                targetFOV,
                Time.deltaTime * zoomSpeed
            );

            Vector3 currentOffset = transposer.FollowOffset;
            Vector3 targetOffset = new Vector3(currentOffset.x, targetFollowY, currentOffset.z);

            transposer.FollowOffset = Vector3.Lerp(
                currentOffset,
                targetOffset,
                Time.deltaTime * zoomSpeed
            );

            yield return null;
        }
    }

    private void OnDestroy()
    {
        if (scoreDisplay != null)
            scoreDisplay.OnScoreChanged -= HandleScoreChanged;
    }
}

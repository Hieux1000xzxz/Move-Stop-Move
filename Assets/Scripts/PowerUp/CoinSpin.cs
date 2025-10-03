using UnityEngine;

public class CoinSpin : MonoBehaviour
{
    [Header("Spin Settings")]
    [SerializeField] private float spinSpeed = 180f; // độ/giây
    [SerializeField] private Vector3 rotationAxis = Vector3.up; // trục xoay (mặc định Y)

    [Header("Float Settings")]
    [SerializeField] private float floatHeight = 0.2f;   // biên độ nhấp nhô
    [SerializeField] private float floatSpeed = 2f;      // tốc độ nhấp nhô

    private Vector3 startPos;

    private void OnEnable()
    {
        // Lưu vị trí ban đầu để nhấp nhô quanh đó
        startPos = transform.localPosition;
    }

    private void Update()
    {
        // Xoay quanh trục
        transform.Rotate(rotationAxis.normalized, spinSpeed * Time.deltaTime, Space.Self);

        // Nhấp nhô lên xuống
        float newY = startPos.y + Mathf.Sin(Time.time * floatSpeed) * floatHeight;
        transform.localPosition = new Vector3(transform.localPosition.x, newY, transform.localPosition.z);
    }
}
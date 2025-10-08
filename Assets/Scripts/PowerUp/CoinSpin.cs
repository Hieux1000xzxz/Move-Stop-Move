using UnityEngine;

public class CoinSpin : MonoBehaviour
{
    [Header("Spin Settings")]
    [SerializeField] private float spinSpeed = 180f; 
    [SerializeField] private Vector3 rotationAxis = Vector3.up; 

    [Header("Float Settings")]
    [SerializeField] private float floatHeight = 0.2f;   
    [SerializeField] private float floatSpeed = 2f;      

    private Vector3 startPos;

    private void OnEnable()
    {
        startPos = transform.localPosition;
    }

    private void Update()
    {
        transform.Rotate(rotationAxis.normalized, spinSpeed * Time.deltaTime, Space.Self);

        float newY = startPos.y + Mathf.Sin(Time.time * floatSpeed) * floatHeight;
        transform.localPosition = new Vector3(transform.localPosition.x, newY, transform.localPosition.z);
    }
}
using UnityEngine;

public class Arrow : WeaponBase
{
    [SerializeField] private Transform arrowModel;
    [SerializeField] private float spinSpeed = 720f;
    private void Update()
    {
        if (rb != null && rb.linearVelocity.sqrMagnitude > 0.01f)
        {
            if (arrowModel != null)
            {
                arrowModel.forward = rb.linearVelocity.normalized;

                arrowModel.Rotate(rb.linearVelocity.normalized, spinSpeed * Time.deltaTime, Space.World);
            }
        }
    }
}

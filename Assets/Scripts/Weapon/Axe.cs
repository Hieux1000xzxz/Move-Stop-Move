using UnityEngine;

public class Axe : WeaponBase
{
    [SerializeField] private Transform axeModel;
    [SerializeField] private Vector3 rotationAxis = Vector3.forward;
    [SerializeField] private float spinSpeed = 720f;

    private void Update()
    {
       
        if (rb != null && rb.linearVelocity.sqrMagnitude > 0.01f && axeModel != null)
            axeModel.Rotate(rotationAxis, spinSpeed * Time.deltaTime, Space.Self);
    }
}

using System.Collections;
using System.Globalization;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

[RequireComponent(typeof(ClientNetworkTransform))]

public class Player : CharacterBase
{
    [SerializeField] private FloatingJoystick joystick;
    public FloatingJoystick Joystick => joystick;
    public bool isMovingInput;
    public NetworkVariable<float> NetSpeed = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    protected override void Start()
    {
        base.Start();

        if (IsOwner) 
        {
            GameManager.Instance.BindCameraToPlayer(transform);
        }
    }
    protected override void Update()
    {
        base.Update();

        if (!IsOwner) return;

        Vector3 input = GetMovementInput();
        isMovingInput = input.magnitude > 0.01f;

        // ghi tốc độ vào NetSpeed
        NetSpeed.Value = input.magnitude * moveSpeed;

        if (isMovingInput && currentState == CharacterState.Attack)
        {
            EndAttack();
            ChangeState(CharacterState.Move);
        }
    }

    
    private float smoothSpeed = 0f;

    protected override void UpdateAnimator()
    {
        if (animator == null) return;

        float targetSpeed;

        if (IsOwner)
        {
            targetSpeed = isMovingInput ? moveSpeed : 0f;  // local input
        }
        else
        {
            targetSpeed = NetSpeed.Value;                 
        }
        
        smoothSpeed = Mathf.Lerp(smoothSpeed, targetSpeed, Time.deltaTime * 15f);

        animator.SetFloat("Speed", smoothSpeed);         
        
        bool attackingNow = IsOwner ? isAttacking : NetIsAttacking.Value;
        animator.SetBool("IsAttacking", attackingNow);
    }

    
    protected override void OnTargetLost(Transform lostTarget)
    {
        base.OnTargetLost(lostTarget);
        if (isMovingInput && currentState == CharacterState.Attack)
        {
            ChangeState(CharacterState.Move);
        }
    }

    protected override void OnNewTargetFound(Transform newTarget)
    {
        base.OnNewTargetFound(newTarget);

        if (!IsServer) return;

        if (!isMovingInput)
        {
            float distance = Vector3.Distance(transform.position, newTarget.position);
            if (distance <= attackRange)
            {
                attackTarget = newTarget;
                ChangeState(CharacterState.Attack);
            }
        }
    }

    public override Vector3 GetMovementInput()
    {
        return new Vector3(joystick.Horizontal, 0f, joystick.Vertical);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        Debug.Log($"{name} spawned for ClientId={OwnerClientId}, IsOwner={IsOwner}");
        if (IsOwner)
        {
            GameManager.Instance.BindCameraToPlayer(transform);
            GameManager.Instance.BindJoystick(this);
            GameManager.Instance.BindKillScoreDisplay(scoreDisplay);
            ulong clientId = OwnerClientId;
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

            transform.position = spawnPos;
        }

        if (IsServer)
        {
            StartCoroutine(DeferredRegister());
        }
    }

    private IEnumerator DeferredRegister()
    {
        yield return null; 
        GameManager.Instance.RegisterPlayerInGame(this.networkObject);
    }
    protected override void Move(Vector3 direction)
    {
        if (isDead) return;

        if (direction.magnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);

            transform.position += direction.normalized * moveSpeed * Time.deltaTime;
        }
    }

    public void SetJoystick(FloatingJoystick js)
    {
        joystick = js;
    }

    private void OnDestroy()
    {
        if (IsServer)
        {
            GameManager.Instance.UnregisterPlayerInGame(this.networkObject);
        }
    }
    private void OnDisable()
    {
        if (IsServer)
        {
            GameManager.Instance.UnregisterPlayerInGame(this.networkObject);
        }
    }
}
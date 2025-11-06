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
    
    private float lastMoveInputTime = 0f;
    private float smoothSpeed = 0f;
    
    [SerializeField] private float minIdleDelay = 0.08f; 
    
    public NetworkVariable<float> NetSpeed = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public static Player Local { get; private set; }

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
        //only the player is controlled
        if (!IsOwner) return;

        Vector3 input = GetMovementInput();
        isMovingInput = input.magnitude > 0.01f;
        
        if (isMovingInput)
        {
            lastMoveInputTime = Time.time;
        }
        
        NetSpeed.Value = input.magnitude * MoveSpeed;
        
        if (NetIsMoving.Value != isMovingInput)
            NetIsMoving.Value = isMovingInput;

        if (isMovingInput && currentState == CharacterState.Attack)
        {
            RequestEndAttackServerRpc();
        }

    }


    [ServerRpc]
    private void RequestEndAttackServerRpc()
    {
        EndAttack(true);
    }
    
    protected override void UpdateAnimator()
    {
        if (animator == null) return;

        float targetSpeed;

        if (IsOwner)
        {
            bool effectiveMoving = isMovingInput || (Time.time - lastMoveInputTime < minIdleDelay);
            targetSpeed = effectiveMoving ? MoveSpeed : 0f;
        }
        else
        {
            targetSpeed = NetSpeed.Value;                 
        }
        
        smoothSpeed = Mathf.Lerp(smoothSpeed, targetSpeed, Time.deltaTime * 25f);

        animator.SetFloat("Speed", smoothSpeed);         
        
        bool attackingNow = IsOwner ? isAttacking : NetIsAttacking.Value;
        animator.SetBool("IsAttacking", attackingNow);
    }
    
    public override Vector3 GetMovementInput()
    {
        return new Vector3(joystick.Horizontal, 0f, joystick.Vertical);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsOwner)
        {
            Local = this;
            
            GameManager.Instance.BindCameraToPlayer(transform);
            GameManager.Instance.BindJoystick(this);
            GameManager.Instance.BindKillScoreDisplay(scoreDisplay);
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
        GameManager.Instance.RegisterKillScore(this.networkObject, scoreDisplay);
    }
    protected override void Move(Vector3 direction)
    {
        if (isDead) return;

        if (direction.magnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);

            transform.position += direction.normalized * MoveSpeed * Time.deltaTime;
        }
    }

    public void SetJoystick(FloatingJoystick js)
    {
        joystick = js;
    }
    
    private new void  OnDisable()
    {
        if (IsServer)
        {
            GameManager.Instance.UnregisterPlayerInGame(this.networkObject);
        }
    }
}
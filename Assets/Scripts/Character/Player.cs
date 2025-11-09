using System.Collections;
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

    public static Player Local { get; private set; }

    protected override void Start()
    {
        base.Start();

        bool isMultiplayer = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        if (!isMultiplayer || IsOwner)
        {
            GameManager.Instance.BindCameraToPlayer(transform);
        }
    }

    protected override void Update()
    {
        bool isMultiplayer = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (isMultiplayer && !IsOwner) return;

        Vector3 input = GetMovementInput();
        isMovingInput = input.magnitude > 0.01f;

        if (isMovingInput)
        {
            detectedTarget = null;
            attackTarget = null;

            lastMoveInputTime = Time.time;

            if (currentState == CharacterState.Attack || isAttacking)
            {
                EndAttack(true);
                if (isMultiplayer) RequestEndAttackServerRpc();
            }
        }

        base.Update();
    }

    protected override void UpdateAnimator()
    {
        if (NetworkAnimator == null || NetworkAnimator.Animator == null) return;

        bool isMultiplayer = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (isMultiplayer && !IsOwner) return;

        float targetSpeed;
        bool effectiveMoving = isMovingInput || (Time.time - lastMoveInputTime < minIdleDelay);
        targetSpeed = effectiveMoving ? MoveSpeed : 0f;

        smoothSpeed = Mathf.Lerp(smoothSpeed, targetSpeed, Time.deltaTime * 25f);

        SetAnimationSpeed(smoothSpeed);
        SetAttackAnimation(isAttacking);
    }

    protected override bool IsMovingNow()
    {
        return isMovingInput;
    }

    [ServerRpc]
    private void RequestEndAttackServerRpc()
    {
        EndAttack(true);
    }

    public override Vector3 GetMovementInput()
    {
        if (isAttacking) return Vector3.zero;
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
        if (isDead || isAttacking) return;

        bool isMultiplayer = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (isMultiplayer && !IsOwner) return;

        if (direction.magnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);

            transform.position += direction * (MoveSpeed * Time.deltaTime);
        }
    }

    public void SetJoystick(FloatingJoystick js)
    {
        joystick = js;
    }

    private new void OnDisable()
    {
        bool isMultiplayer = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        if (isMultiplayer && IsServer)
        {
            GameManager.Instance.UnregisterPlayerInGame(this.networkObject);
        }
    }
}
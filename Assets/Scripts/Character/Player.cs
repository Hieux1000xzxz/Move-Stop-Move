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

    [SerializeField] private float minIdleDelay = 0.15f;
    [SerializeField] private float speedSmoothTime = 0.1f;
    private float speedVelocity = 0f;
    public static Player Local { get; private set; }

    protected override void Start()
    {
        base.Start();

        SetAnimatorParameters(netSpeed.Value, netIsAttacking.Value);

        if (ShouldProcessInput)
        {
            GameManager.Instance.BindCameraToPlayer(transform);
        }
    }

    protected override void Update()
    {
        if (!ShouldProcessInput) return;

        HandleMovementInput();

        base.Update();
    }

    private void HandleMovementInput()
    {
        Vector3 input = GetMovementInput();
        isMovingInput = input.magnitude > 0.01f;

        if (isMovingInput)
        {
            detectedTarget = null;
            attackTarget = null;
            lastMoveInputTime = Time.time;

            if (ShouldCancelAttack())
            {
                EndAttack(true);
                if (IsMultiplayer) RequestEndAttackServerRpc();
            }
        }
    }

    private bool ShouldCancelAttack()
    {
        return currentState == CharacterState.Attack || isAttacking;
    }

    protected override void UpdateAnimator()
    {
        if (NetworkAnimator == null || animator == null) return;

        if (ShouldProcessInput)
        {
            CalculateAndApplyAnimation();
        }
    }

    private void CalculateAndApplyAnimation()
    {
        float targetSpeed = CalculateTargetSpeed();
        smoothSpeed = Mathf.SmoothDamp(smoothSpeed, targetSpeed, ref speedVelocity, speedSmoothTime);

        SetAnimatorParameters(smoothSpeed, isAttacking);
        SyncAnimationToNetwork(smoothSpeed, isAttacking);
    }

    private float CalculateTargetSpeed()
    {
        bool effectiveMoving = isMovingInput || (Time.time - lastMoveInputTime < minIdleDelay);
        return effectiveMoving ? MoveSpeed : 0f;
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

            smoothSpeed = 0f;
            netSpeed.Value = 0f;
            netIsAttacking.Value = false;
        }
        else
        {
            netSpeed.OnValueChanged += OnNetSpeedChanged;
            netIsAttacking.OnValueChanged += OnNetIsAttackingChanged;

            if (animator != null)
            {
                animator.SetFloat("Speed", netSpeed.Value);
                animator.SetBool("IsAttacking", netIsAttacking.Value);
            }
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
        if (isDead || isAttacking || !ShouldProcessInput) return;

        if (direction.magnitude > 0.01f)
        {
            RotateTowards(direction);
            MoveForward(direction);
        }
    }

    private void MoveForward(Vector3 direction)
    {
        transform.position += direction * (MoveSpeed * Time.deltaTime);
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

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        if (!IsOwner)
        {
            netSpeed.OnValueChanged -= OnNetSpeedChanged;
            netIsAttacking.OnValueChanged -= OnNetIsAttackingChanged;
        }
    }

    private void OnNetSpeedChanged(float oldValue, float newValue)
    {
        if (!IsOwner && animator != null)
        {
            animator.SetFloat("Speed", newValue);
        }
    }

    private void OnNetIsAttackingChanged(bool oldValue, bool newValue)
    {
        if (!IsOwner && animator != null)
        {
            animator.SetBool("IsAttacking", newValue);
        }
    }
}
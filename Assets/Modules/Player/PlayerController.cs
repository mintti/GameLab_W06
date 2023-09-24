using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public class PlayerController : MonoBehaviour
{// 컴포넌트들
    [Header("Components")]
    private Rigidbody2D body;
    private Animator myAnimator;

    // 콜라이더 설정
    [Header("Collider")]
    [SerializeField] [Tooltip("땅 체크 콜라이더의 길이")] private float groundLength = 0.95f;
    [SerializeField] [Tooltip("땅 체크 콜라이더 간의 거리")] private Vector3 colliderOffset;

    // 이동 값
    [Header("MoveValue")]
    [SerializeField, Range(0f, 20f)] public float maxSpeed = 10f;
    [SerializeField, Range(0f, 100f)] public float maxAcceleration = 52f;
    [SerializeField, Range(0f, 100f)] public float maxDecceleration = 52f;
    [SerializeField, Range(0f, 100f)] public float maxTurnSpeed = 80f;
    [SerializeField, Range(0f, 100f)] public float maxAirAcceleration;
    [SerializeField, Range(0f, 100f)] public float maxAirDeceleration;
    [SerializeField, Range(0f, 100f)] public float maxAirTurnSpeed = 80f;
    [SerializeField, Range(0f, 100f)] public float runSpeedMultiplier = 1f;
    [SerializeField] private float friction;
    
    [SerializeField] public bool onStretchRun;
    [SerializeField] public float startStretchRunSpeed;
    [SerializeField] public float maxStretchRunSpeed;

    // 점프 관련
    [Header("JumpValue")]
    [SerializeField, Range(2f, 5.5f)] [Tooltip("최대 점프 높이")] public float jumpHeight = 7.3f;
    [SerializeField, Range(0.2f, 1.25f)] [Tooltip("점프 높이에 도달하기까지 걸리는 시간")] public float timeToJumpApex;
    [SerializeField, Range(0f, 5f)] [Tooltip("위로 이동 시 적용되는 중력 배율")] public float upwardMovementMultiplier = 1f;
    [SerializeField, Range(1f, 10f)] [Tooltip("아래로 이동 시 적용되는 중력 배율")] public float downwardMovementMultiplier = 6.17f;
    [SerializeField, Range(0, 2)] [Tooltip("공중에서 몇 번 점프할 수 있는지")] public int maxAirJumps = 0;
    
    [SerializeField] public bool activeStretchJump;
    [SerializeField] public bool onStretchJump;
    [SerializeField] public float startStretchJumpSpeed;
    [SerializeField] public float maxStretchJumpSpeed;
        
    // 기울기
    [Header("TiltValue")]
    [SerializeField, Tooltip("캐릭터가 얼마나 기울어질지")] public float maxTilt;
    [SerializeField, Tooltip("캐릭터의 기울기 속도")] public float tiltSpeed;

    // 옵션
    [Header("Option")]
    public bool useAcceleration;
    [Tooltip("점프 버튼을 놓았을 때 캐릭터가 떨어져야 하는가?")] public bool variablejumpHeight;
    [SerializeField, Range(1f, 10f)] [Tooltip("점프 버튼을 놓았을 때 적용되는 중력 배율")] public float jumpCutOff;
    [SerializeField] [Tooltip("캐릭터가 낙하할 수 있는 가장 빠른 속도")] public float speedLimit;
    [SerializeField, Range(0f, 5f)] [Tooltip("코요태임이 얼마나 지속되어야 하는가?")] public float coyoteTime = 0.15f;
    [SerializeField, Range(0f, 0.3f)] [Tooltip("땅으로부터 얼마나 떨어진 위치에서 점프 입력을 받아들일지")] public float jumpBuffer = 0.15f;

    // 계산 값
    [Header("Calcurator")]
    public float directionX;
    private Vector2 desiredVelocity;
    
    
    public Vector2 _velocity;
    public Vector2 Velocity
    {
        get => _velocity;
        set
        {
            _velocity = value;
            
            if (activeStretchJump && canJumpMotion)
            {
                if (!onStretchJump)
                {
                    if (_velocity.y < -15)
                    {
                        onStretchJump = true;
                        myAnimator.SetTrigger("Stretch");
                        Debug.Log("Stretch");
                    }
                }
            }

            if (!onStretchHorizon)
            {
                if (Math.Abs(_velocity.x) >= 3)
                {
                    onStretchHorizon = true;
                    myAnimator.SetTrigger("StretchHorizon"); 
                }
            }
            else if(Math.Abs(_velocity.x) < 1)
            {
                onStretchHorizon = false;
                myAnimator.SetTrigger("StretchHorizonCancel"); 
            }
        }
    }
    
    public bool onStretchHorizon;
    

    public void OnHyperSquashMode()
    {
        hyperSquashMode = true;
    }
    
    
    public bool canJumpMotion;
    public bool hyperSquashMode;
    private float maxSpeedChange;
    private float acceleration;
    private float deceleration;
    private float turnSpeed;
    public float runningSpeed;
    private float defaultGravityScale;
    public float gravMultiplier;



    public float _jumpSpeed;
    
    // 레이어 마스크
    [Header("LayerMask")]
    [SerializeField] [Tooltip("땅으로 인식할 레이어")] private LayerMask groundLayer;

    // 현재 상태
    [Header("CurrentState")]
    private bool _onGround;

    public bool OnGround
    {
        get => _onGround;
        set
        {
            _onGround = value;

            if (_onGround && activeStretchJump && onStretchJump)
            {
                if (hyperSquashMode)
                {
                    myAnimator.SetTrigger("HyperSquash");
                    hyperSquashMode = false;
                }
                else
                {
                    myAnimator.SetTrigger("Squash");
                }
                
                onStretchJump = false;
                canJumpMotion = false;
            }
        }
    }
    
    public bool pressingKey;
    public bool canJumpAgain = false;
    public bool onMud = false;
    public bool onIce = false; 
    private bool desiredJump;
    private float jumpBufferCounter;
    private float coyoteTimeCounter = 0;
    private bool pressingJump;
    private bool currentlyJumping;

    private int airJumpCount = 0;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        myAnimator = GetComponent<Animator>();
        defaultGravityScale = 1f;
    }

    public void OnMove(InputValue value)
    {
        directionX = value.Get<float>();
        myAnimator.SetFloat("XDir", directionX);
    }

    public void OnJump(InputValue value)
    {
        // 이 함수는 점프 버튼 (예: 스페이스바 또는 A 버튼) 중 하나가 눌렸을 때 호출됩니다.

        // 점프 버튼이 눌렸을 때, 점프를 원하는 상태로 설정합니다.
        // 시작 및 취소된 컨텍스트를 사용하여 현재 버튼을 누르고 있는지 여부를 파악합니다.
        if (value.isPressed)
        {
            desiredJump = true;
        }
    }

    public void OnRun(InputValue value)
    {
        if (value.isPressed)
        {
            maxTilt = 30f;
            runSpeedMultiplier = 1.5f;
        }
        else
        {
            maxTilt = 7.8f;
            runSpeedMultiplier = 1f;
        }
    }

    private void Update()
    {
        // 이동 상태 애니메이션
        runningSpeed = Mathf.Clamp(Mathf.Abs(Velocity.x), 0, maxSpeed);
        myAnimator.SetFloat("playerSpeed", runningSpeed);

        // 지면 상태 체크
        OnGround = Physics2D.Raycast(transform.position + colliderOffset, Vector2.down, groundLength, groundLayer) || Physics2D.Raycast(transform.position - colliderOffset, Vector2.down, groundLength, groundLayer) || Physics2D.Raycast(transform.position, Vector2.down, groundLength, groundLayer);

        if (directionX != 0)
        {
            transform.localScale = new Vector3(directionX > 0 ? 1 : -1, 1, 1);
            pressingKey = true;
        }
        else
        {
            pressingKey = false;
        }

        if (!onIce && !onMud)
        {
            desiredVelocity = new Vector2(directionX, 0f) * Mathf.Max(maxSpeed - friction, 0f) * runSpeedMultiplier;
        }

        if (onIce)
        {
            desiredVelocity = new Vector2(directionX, 0f) * Mathf.Max(maxSpeed * 1.5f - friction, 0f) * runSpeedMultiplier;
        }

        if (onMud)
        {
            desiredVelocity = new Vector2(directionX, 0f) * Mathf.Max(maxSpeed * 0.5f - friction, 0f) * runSpeedMultiplier;
        }


        // 점프 버퍼를 통해 점프를 대기열에 넣어 다음에 땅에 닿았을 때 자동으로 점프를 수행할 수 있게 합니다.
        if (jumpBuffer > 0)
        {
            // desireJump 상태를 바로 끄는 대신 시간을 세어 올립니다.
            // 이 동안 DoAJump 함수는 계속 호출될 것입니다.
            if (desiredJump)
            {
                jumpBufferCounter += Time.deltaTime;

                if (jumpBufferCounter > jumpBuffer)
                {
                    // 시간이 점프 버퍼를 초과하면 desireJump를 끕니다.
                    desiredJump = false;
                    jumpBufferCounter = 0;
                }
            }
        }

        // 땅 위에 없고 현재 점프 중이 아니라면, 플랫폼 가장자리에서 떨어진 것입니다.
        // 그래서 코요태임 카운터를 시작합니다.
        if (!currentlyJumping && !OnGround)
        {
            coyoteTimeCounter += Time.deltaTime;
        }
        else
        {
            // 땅을 밟거나 점프할 때마다 초기화합니다.
            coyoteTimeCounter = 0;
        }
    }

    private void FixedUpdate()
    {
        Velocity = body.velocity;

        if (useAcceleration)
        {
            runWithAcceleration();
        }
        else
        {
            if (OnGround)
            {
                runWithoutAcceleration();
            }
            else
            {
                runWithAcceleration();
            }
        }

        // desireJump가 true인 동안 계속 점프를 시도합니다.
        if (desiredJump)
        {
            DoAJump();
            body.velocity = Velocity;

            // 이 프레임에 중력 계산을 건너뛰어 currentlyJumping이 꺼지지 않도록 합니다.
            // 이렇게 하면 코요태임 더블 점프 버그가 발생하지 않습니다.
            return;
        }
        calculateGravity();
    }

    private void OnDrawGizmos()
    {
        // 디버그 목적으로 화면에 지면 콜라이더를 그립니다.
        if (OnGround) { Gizmos.color = Color.green; } else { Gizmos.color = Color.red; }
        Gizmos.DrawLine(transform.position + colliderOffset, transform.position + colliderOffset + Vector3.down * groundLength);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * groundLength);
        Gizmos.DrawLine(transform.position - colliderOffset, transform.position - colliderOffset + Vector3.down * groundLength);
    }

    private void tiltCharacter()
    {
        // 캐릭터가 현재 어느 방향으로 달리고 있는지 확인하고 해당 방향으로 기울입니다.
        float directionToTilt = 0;
        if (Velocity.x != 0)
        {
            directionToTilt = Mathf.Sign(Velocity.x);
        }

        // 캐릭터가 기울어질 방향을 나타내는 벡터를 만듭니다.
        Vector3 targetRotVector = new Vector3(0, 0, Mathf.Lerp(-maxTilt, maxTilt, Mathf.InverseLerp(-1, 1, directionToTilt)));

        // 그리고 그 방향으로 캐릭터를 회전시킵니다.
        myAnimator.transform.rotation = Quaternion.RotateTowards(myAnimator.transform.rotation, Quaternion.Euler(-targetRotVector), tiltSpeed * Time.deltaTime);
    }

    private void calculateGravity()
    {
        // 캐릭터의 Y 방향 이동에 따라 중력을 조정합니다.

        // 캐릭터가 위로 이동 중이면...
        if (body.velocity.y > 0.01f)
        {
            if (OnGround)
            {
                // 바닥 위에 서 있는 경우 중력을 변경하지 않습니다.
                gravMultiplier = defaultGravityScale;
            }
            else
            {
                // 변수 점프 높이를 사용 중인 경우...
                if (variablejumpHeight)
                {
                    // 플레이어가 점프 버튼을 누르고 점프 중인 경우 위로 이동하는 중력 배율을 적용합니다.
                    if (pressingJump && currentlyJumping)
                    {
                        gravMultiplier = upwardMovementMultiplier;
                    }
                    // 그렇지 않으면 점프 버튼을 놓았을 때 특별한 아래로 이동 중력 배율을 적용합니다.
                    else
                    {
                        gravMultiplier = jumpCutOff;
                    }
                }
                else
                {
                    gravMultiplier = upwardMovementMultiplier;
                }
            }
        }

        // 아래로 이동 중이면...
        else if (body.velocity.y < -0.01f)
        {
            if (OnGround)
            {
                // 바닥 위에 서 있는 경우 중력을 변경하지 않습니다.
                gravMultiplier = defaultGravityScale;
            }
            else
            {
                // 그 외의 경우 캐릭터가 다시 땅으로 내려올 때 아래로 이동 중력 배율을 적용합니다.
                gravMultiplier = downwardMovementMultiplier;
            }
        }
        // 수직으로 움직이지 않는 경우
        else
        {
            if (OnGround)
            {
                currentlyJumping = false;
            }

            gravMultiplier = defaultGravityScale;
        }

        // 캐릭터의 Rigidbody의 속도를 설정합니다.
        // 속도 제한 옵션을 고려하여 Y 변수를 -speedLimit와 100 사이로 클램핑합니다.
        body.velocity = new Vector3(Velocity.x, Mathf.Clamp(Velocity.y, -speedLimit, 100));
    }

    private void DoAJump()
    {
        if (OnGround || (coyoteTimeCounter > 0.03f && coyoteTimeCounter < coyoteTime) || airJumpCount < maxAirJumps)
        {
            canJumpMotion = true;

            if (onStretchJump)
            {
                myAnimator.SetTrigger("StretchCancel");
            }
            
            desiredJump = false;

            if (OnGround)
            {
                canJumpAgain = true;
                airJumpCount = 0;
            }
            else
            {
                airJumpCount++;
            }

            if(!onIce && !onMud)
            {
                _jumpSpeed = Mathf.Sqrt(-2f * Physics2D.gravity.y * body.gravityScale * jumpHeight);
            }

            if (onIce)
            {
                _jumpSpeed = Mathf.Sqrt(-2f * Physics2D.gravity.y * body.gravityScale * jumpHeight * 1.3f);
            }

            if (onMud)
            {
                _jumpSpeed = Mathf.Sqrt(-2f * Physics2D.gravity.y * body.gravityScale * jumpHeight * 0.7f);
            }


            float jumpDuration = Mathf.Sqrt((2f * jumpHeight) / (-Physics2D.gravity.y * body.gravityScale));
            float calculatedJumpDistance = _jumpSpeed * jumpDuration;

            if (Velocity.y > 0f)
            {
                _jumpSpeed = Mathf.Max(_jumpSpeed - Velocity.y, 0f);
            }
            else if (Velocity.y < 0f)
            {
                _jumpSpeed += Mathf.Abs(body.velocity.y);
            }

            Velocity = new Vector2(Velocity.x, Velocity.y + _jumpSpeed);
            currentlyJumping = true;

            myAnimator.ResetTrigger("Landed");
            myAnimator.SetTrigger("Jump");
        }

        if (jumpBuffer == 0)
        {
            desiredJump = false;
        }
    }

    public void bounceUp(float bounceAmount)
    {
        // 스프링 패드에서 사용됩니다.
        body.AddForce(Vector2.up * bounceAmount, ForceMode2D.Impulse);
    }

    private void setPhysics()
    {
        Vector2 newGravity = new Vector2(0, (-2 * jumpHeight) / (timeToJumpApex * timeToJumpApex));

        // 중력 스케일을 적용하여 캐릭터의 Rigidbody 중력을 설정합니다.
        body.gravityScale = (newGravity.y / Physics2D.gravity.y) * gravMultiplier;
    }

    private void runWithAcceleration()
    {
        acceleration = OnGround ? maxAcceleration : maxAirAcceleration;
        deceleration = OnGround ? maxDecceleration : maxAirDeceleration;
        turnSpeed = OnGround ? maxTurnSpeed : maxAirTurnSpeed;

        if (pressingKey)
        {
            // 입력 방향의 부호 (양수 또는 음수)와 현재 이동 방향의 부호가 일치하지 않는 경우, 회전 중이므로 회전 속도를 사용합니다.
            if (Mathf.Sign(directionX) != Mathf.Sign(Velocity.x))
            {
                maxSpeedChange = turnSpeed * Time.deltaTime;
            }
            else
            {
                // 일치하는 경우, 단순히 가속도 스탯을 사용하여 달립니다.
                maxSpeedChange = acceleration * Time.deltaTime;
            }
        }
        else
        {
            // 방향 입력이 없는 경우 감속도 스탯을 사용합니다.
            maxSpeedChange = deceleration * Time.deltaTime;
        }

        // 현재 속도를 목표 속도로 서서히 변경합니다. 변경 속도는 위에서 계산한 값으로 결정됩니다.
        Velocity = new Vector2(Mathf.MoveTowards(Velocity.x, desiredVelocity.x, maxSpeedChange), Velocity.y);

        // 새로운 속도를 Rigidbody에 적용합니다.
        body.velocity = Velocity;
    }

    private void runWithoutAcceleration()
    {
        // 가속도 및 감속도를 사용하지 않을 경우, 목표 속도 (방향 * 최대 속도)를 바로 Rigidbody에 전달합니다.
        Velocity = new Vector2(desiredVelocity.x, Velocity.y);
        body.velocity = Velocity;
    }
}
